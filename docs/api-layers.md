# API layer structure

Ferrite has one base API implementation at layer 229. It accepts exactly layers
150, 214–225, and 227–229. Negotiation checks membership in that set; it does not
clamp an unsupported layer to a nearby value.

## Schema ownership

| Input | Purpose |
|---|---|
| [`Ferrite.TL/baseLayer.tl`](../Ferrite.TL/baseLayer.tl) | Base declarations used to generate the server's slim TL bindings. |
| [`LayerSchemaCatalog.BaseLayer`](../Ferrite.TL.Schema/LayerSchemaCatalog.cs) | The single base-layer number. The catalog discovers historical layers from embedded schema resources. |
| [`Ferrite.TL.Schema/Schemas/layerN.tl`](../Ferrite.TL.Schema/Schemas/) | Historical overrides used with the base schema to reconstruct each published layer. |
| [`introductions.tsv`](../Ferrite.TL.Schema/Schemas/introductions.tsv) | Introduction layers used to exclude symbols absent from an older schema. |
| [`dispositions.tsv`](../Ferrite.TL.Schema/Schemas/dispositions.tsv) | Generated response-edge policies, including references to reviewed semantic converters. |
| `Ferrite.TL/layerN.tl` | Historical declarations needed for generated bindings and compatibility requests, including older constructors still sent by current clients. These files do not determine which layers can be negotiated. |

The build includes historical TL inputs through a wildcard. The source generator
processes `mtproto.tl`, `baseLayer.tl`, and `e2eChain.tl` first, then historical
inputs in numeric layer order. This keeps generated type ownership stable when
multiple files contain the same declaration.

## Conversion and dispatch

[`LayerRequestNormalizer`](../Ferrite.Core/Execution/LayerRequestNormalizer.cs)
uses the request mapper to upgrade a request through each consecutive published
edge toward the base. [`PublishedRequestMappers`](../Ferrite.TL.Schema/PublishedRequestMappers.cs)
builds that ascending route from the catalog. Handler lookup uses the resulting
constructor and does not include a layer number.

The response route walks the same catalog in reverse. Results and updates are
transformed for the recipient's stored layer, including recipients on different
layers in one fan-out.

Schema-compatible changes are handled by the conversion engines. Changes in
meaning belong in [`Ferrite.TL.Schema/Conversions/`](../Ferrite.TL.Schema/Conversions/):
polls, reply markup, forum topics, and other concerns each have one owner holding
their upgrade and downgrade functions. Both directions use
[`LayerConversionContext`](../Ferrite.TL.Schema/LayerConversionContext.cs) for
field copying, nested-object conversion, and vector elements.
[`PublishedRequestConverters`](../Ferrite.TL.Schema/PublishedRequestConverters.cs)
declares request-edge converters and defaults;
[`PublishedLayerTransformRegistry`](../Ferrite.TL.Schema/PublishedLayerTransformRegistry.cs)
binds response converter names to their implementations. Request field-consumption
checks and response disposition policies remain with their respective engines.

## Adding a new base layer

1. Obtain and pin the exact new API schema. Update `Ferrite.TL/baseLayer.tl` and
   change `LayerSchemaCatalog.BaseLayer` to its layer number.
2. Regenerate historical overrides against that base, retaining the previous base
   as `Ferrite.TL.Schema/Schemas/layerN.tl`. Regenerate `introductions.tsv` and
   `dispositions.tsv`, and review the resulting schema and edge changes. These
   are committed inputs; compiling consumes them rather than reconstructing
   upstream history.
3. Regenerate the historical declarations needed by the server in
   `Ferrite.TL/layerN.tl`. The project wildcard includes new files automatically.
   Negotiation and both routes follow the catalog without separate list edits.
4. For every semantic change, add or update the reviewed converter for that symbol
   on the new edge in its existing semantic owner. Review both request upgrades
   and response downgrades, then register the required converters and defaults.
   Unchanged edges retain their existing converters.
5. Rebuild the generated bindings and reconcile every new method with a handler
   or a disabled registration.
   [`DisabledMethods`](../Ferrite.Core/Execution/Functions/BaseLayer/DisabledMethods.cs)
   disables the `bots`, `fragment`, `payments`, `premium`, `smsjobs` and `stories`
   namespaces whole and other methods by TL name. Their constructor ids come from
   the catalog, so a changed id needs no edit. Then verify historical schema
   reconstruction, conversion coverage, and wire results. Exercise clients at the
   oldest supported layer, a middle layer, and the new base, including mixed-layer
   recipients.

Adding schema metadata creates a route; it is not proof that the new layer's
behavior is complete. Semantic conversion and client checks must pass before
publishing the new supported set.
