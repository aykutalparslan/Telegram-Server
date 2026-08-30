"""Console dashboard for the public launcher.

The launcher is one public command that has to run on a host nobody set up by
hand, so this draws with plain ANSI escapes and the standard library only. It
repaints in place in the normal buffer rather than the alternate screen: the
last frame stays on the terminal after the run, which is what a demo wants.
"""

from __future__ import annotations

import datetime as dt
import os
import shutil
import sys
import threading
import time
from typing import Any, Iterable, Sequence

RESET = "\x1b[0m"
DIM = "\x1b[2m"
BOLD = "\x1b[1m"
RED = "\x1b[31m"
GREEN = "\x1b[32m"
YELLOW = "\x1b[33m"
BLUE = "\x1b[34m"
CYAN = "\x1b[36m"
GREY = "\x1b[90m"

# Every state a resource or an application row can carry, with the glyph and
# colour that report it. Anything unknown falls back to "pending".
STATES: dict[str, tuple[str, str, str]] = {
    "ready": ("*", GREEN, "●"),
    "starting": ("+", YELLOW, "◐"),
    "pending": ("-", GREY, "○"),
    "stopped": ("x", RED, "●"),
    "failed": ("x", RED, "✖"),
}
SPINNER_ASCII = "|/-\\"
SPINNER_UNICODE = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏"
BOX_ASCII = {"h": "-", "v": "|", "tl": "+", "tr": "+", "bl": "+", "br": "+"}
BOX_UNICODE = {"h": "─", "v": "│", "tl": "╭", "tr": "╮",
               "bl": "╰", "br": "╯"}

Chunk = tuple[str, str]
MIN_WIDTH = 60
SPLIT_WIDTH = 88
GRAPH_WIDTH = 38


def visible_length(chunks: Sequence[Chunk]) -> int:
    return sum(len(text) for text, _ in chunks)


def column(text: str, width: int) -> str:
    """A fixed-width cell that always keeps one space before the next one."""
    return clip(text, width - 1).ljust(width)


def clip(text: str, width: int) -> str:
    if width <= 0:
        return ""
    if len(text) <= width:
        return text
    return text[:width - 1] + "…" if width > 1 else text[:width]


class Theme:
    def __init__(self, colour: bool, unicode_ok: bool) -> None:
        self.colour = colour
        self.box = BOX_UNICODE if unicode_ok else BOX_ASCII
        self.spinner = SPINNER_UNICODE if unicode_ok else SPINNER_ASCII
        self.unicode_ok = unicode_ok

    def paint(self, text: str, style: str) -> str:
        if not self.colour or not style or not text:
            return text
        return f"{style}{text}{RESET}"

    def glyph(self, state: str, frame: int) -> Chunk:
        ascii_glyph, style, unicode_glyph = STATES.get(state, STATES["pending"])
        if state == "starting":
            spinner = self.spinner[frame % len(self.spinner)]
            return (spinner, style)
        return (unicode_glyph if self.unicode_ok else ascii_glyph, style)


class Panel:
    """A titled box that renders a fixed number of content rows."""

    def __init__(self, title: str, rows: list[list[Chunk]], height: int,
                 hint: str = "") -> None:
        self.title = title
        self.rows = rows
        self.height = height
        self.hint = hint

    def render(self, theme: Theme, width: int) -> list[str]:
        box = theme.box
        inner = width - 2
        title = clip(f" {self.title} ", max(inner - 2, 0))
        hint = clip(f" {self.hint} ", max(inner - len(title) - 2, 0)) \
            if self.hint else ""
        filler = box["h"] * max(inner - len(title) - len(hint), 0)
        top = (box["tl"] + theme.paint(title, BOLD + CYAN) + filler
               + theme.paint(hint, GREY) + box["tr"])
        lines = [top]
        body = self.rows[-self.height:] if len(self.rows) > self.height \
            else list(self.rows)
        while len(body) < self.height:
            body.append([])
        for row in body:
            painted = ""
            remaining = inner
            for text, style in row:
                if remaining <= 0:
                    break
                text = clip(text, remaining)
                remaining -= len(text)
                painted += theme.paint(text, style)
            lines.append(box["v"] + painted + " " * remaining + box["v"])
        lines.append(box["bl"] + box["h"] * inner + box["br"])
        return lines


def join_side_by_side(left: list[str], right: list[str], gap: int = 1) -> list[str]:
    height = max(len(left), len(right))
    left += [""] * (height - len(left))
    right += [""] * (height - len(right))
    return [a + " " * gap + b for a, b in zip(left, right)]


class Dashboard:
    """Live panels for one launcher invocation.

    Every setter is safe to call from the phase code; a background thread owns
    the repaint so the clock and the spinners keep moving while a phase blocks.
    """

    def __init__(self, *, stream: Any = None, enabled: bool | None = None,
                 title: str = "Ferrite upstream apps") -> None:
        self.stream = stream or sys.stderr
        if enabled is None:
            enabled = (self.stream.isatty()
                       and os.environ.get("NO_COLOR") is None
                       and os.environ.get("TERM", "") not in ("", "dumb"))
        self.enabled = bool(enabled)
        colour = self.enabled and os.environ.get("NO_COLOR") is None
        encoding = (getattr(self.stream, "encoding", "") or "").lower()
        self.theme = Theme(colour, encoding.startswith("utf"))
        self.title = title
        self.lock = threading.RLock()
        self.started = time.monotonic()
        self.frame = 0
        self.painted = 0
        self.width = 0
        self.stopping = threading.Event()
        self.thread: threading.Thread | None = None
        self.run_id = ""
        self.phase = "starting"
        self.phase_state = "starting"
        self.summary = ""
        self.graph: list[dict[str, Any]] = []
        self.apps: list[dict[str, Any]] = []
        self.log_lines: list[str] = []
        self.notes: list[str] = []

    # -- lifecycle ---------------------------------------------------------
    def __enter__(self) -> "Dashboard":
        if self.enabled:
            self.stream.write("\x1b[?25l")
            self.stream.flush()
            self.thread = threading.Thread(target=self._loop, daemon=True)
            self.thread.start()
        return self

    def __exit__(self, *exception: Any) -> None:
        self.stopping.set()
        if self.thread:
            self.thread.join(timeout=2)
        if self.enabled:
            with self.lock:
                self._paint()
                self.stream.write("\x1b[?25h")
                self.stream.flush()

    def _loop(self) -> None:
        while not self.stopping.wait(0.2):
            with self.lock:
                self.frame += 1
                self._paint()

    # -- state -------------------------------------------------------------
    def set_run(self, run_id: str, summary: str = "") -> None:
        with self.lock:
            self.run_id = run_id
            if summary:
                self.summary = summary

    def set_phase(self, phase: str, state: str = "starting") -> None:
        with self.lock:
            self.phase = phase
            self.phase_state = state

    def set_graph(self, rows: Iterable[dict[str, Any]]) -> None:
        with self.lock:
            self.graph = list(rows)

    def set_apps(self, rows: Iterable[dict[str, Any]]) -> None:
        with self.lock:
            self.apps = list(rows)

    def log(self, message: str) -> None:
        stamp = dt.datetime.now().strftime("%H:%M:%S")
        with self.lock:
            self.log_lines.append(f"{stamp} {message}")
            del self.log_lines[:-400]
            self.notes.clear()
            if not self.enabled:
                self.stream.write(f"[{stamp}] {message}\n")
                self.stream.flush()

    def note(self, line: str) -> None:
        """A line of subprocess output; shown only while its phase runs."""
        with self.lock:
            self.notes.append(line.rstrip())
            del self.notes[:-200]
            if not self.enabled:
                self.stream.write(f"    {line}\n")
                self.stream.flush()

    def refresh(self) -> None:
        if not self.enabled:
            return
        with self.lock:
            self._paint()

    # -- rendering ---------------------------------------------------------
    def _paint(self) -> None:
        size = shutil.get_terminal_size(fallback=(100, 30))
        width = max(size.columns, MIN_WIDTH)
        lines = self._frame(width, max(size.lines - 1, 12))
        if width != self.width:
            # A resized terminal invalidates the cursor arithmetic below, so
            # the next frame is printed fresh instead of repainting garbage.
            self.painted = 0
            self.width = width
        out = []
        if self.painted:
            out.append(f"\x1b[{self.painted}A")
        for line in lines:
            out.append("\x1b[2K" + line + "\n")
        self.painted = len(lines)
        self.stream.write("".join(out))
        self.stream.flush()

    def _frame(self, width: int, height_budget: int) -> list[str]:
        header = self._header_panel()
        graph = self._graph_panel()
        apps = self._apps_panel()
        if width >= SPLIT_WIDTH:
            rows = max(graph.height, apps.height)
            graph.height = apps.height = rows
            middle = join_side_by_side(
                graph.render(self.theme, GRAPH_WIDTH),
                apps.render(self.theme, width - GRAPH_WIDTH - 1))
        else:
            middle = (graph.render(self.theme, width)
                      + apps.render(self.theme, width))
        used = len(header.render(self.theme, width)) + len(middle)
        # The log grows to what it has to show and no further, so a quiet run
        # does not leave an empty box filling the terminal.
        available = max(height_budget - used - 2, 3)
        log_rows = min(available, max(len(self.log_lines) + len(self.notes), 6))
        lines = header.render(self.theme, width) + middle
        lines += self._log_panel(log_rows).render(self.theme, width)
        return lines[:height_budget]

    def _header_panel(self) -> Panel:
        elapsed = int(time.monotonic() - self.started)
        clock = f"{elapsed // 60:02d}:{elapsed % 60:02d}"
        row: list[Chunk] = [(" ", "")]
        row.append(self.theme.glyph(self.phase_state, self.frame))
        row.append((f" {self.phase}", BOLD))
        if self.summary:
            row.append((f"   {self.summary}", GREY))
        row.append(("  ", ""))
        return Panel(f"{self.title}{'  ' + self.run_id if self.run_id else ''}",
                     [row], 1, hint=clock)

    def _graph_panel(self) -> Panel:
        rows: list[list[Chunk]] = []
        ready = 0
        for item in self.graph:
            state = item.get("state", "pending")
            ready += state == "ready"
            row: list[Chunk] = [(" ", ""), self.theme.glyph(state, self.frame)]
            row.append((" " + column(item.get("resource", ""), 18), ""))
            port = item.get("port")
            row.append((f"{port or '':>5} ", GREY))
            row.append((state, STATES.get(state, STATES["pending"])[1]))
            rows.append(row)
        if not rows:
            rows.append([("  no graph resources", GREY)])
        hint = f"{ready}/{len(self.graph)}" if self.graph else ""
        return Panel("Graph", rows, len(rows), hint=hint)

    def _apps_panel(self) -> Panel:
        rows: list[list[Chunk]] = []
        ready = 0
        for item in self.apps:
            state = item.get("state", "pending")
            ready += state == "ready"
            row: list[Chunk] = [(" ", ""), self.theme.glyph(state, self.frame)]
            row.append((" " + column(item.get("label", ""), 11), ""))
            row.append((column(item.get("phone", ""), 15), GREY))
            row.append((column(item.get("device", "—"), 22), BLUE))
            row.append((item.get("detail") or state,
                        STATES.get(state, STATES["pending"])[1]))
            rows.append(row)
        if not rows:
            rows.append([("  no applications requested", GREY)])
        hint = f"{ready}/{len(self.apps)}" if self.apps else ""
        return Panel("Apps", rows, len(rows), hint=hint)

    def _log_panel(self, height: int) -> Panel:
        entries = list(self.log_lines)
        if self.notes:
            entries += [f"   {line}" for line in self.notes]
        rows: list[list[Chunk]] = []
        for entry in entries[-height:]:
            stamp, _, rest = entry.partition(" ")
            if len(stamp) == 8 and stamp.count(":") == 2:
                rows.append([(" " + stamp + " ", GREY), (rest, "")])
            else:
                rows.append([(" " + entry, GREY)])
        return Panel("Log", rows, height)
