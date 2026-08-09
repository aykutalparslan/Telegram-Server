# syntax=docker/dockerfile:1
FROM node:24.10.0-bookworm-slim

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /worker
COPY package.json package-lock.json ./
RUN npm ci --omit=dev
COPY src ./src

RUN mkdir -p /recordings && chown node:node /recordings

USER node
EXPOSE 9090/tcp 19350/tcp 40000-40100/udp
CMD ["npm", "start"]
