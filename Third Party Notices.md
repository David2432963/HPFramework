# Third Party Notices

HP Framework redistributes required third-party dependencies so the framework is self-contained. The upstream licenses apply to those components; they are not covered by any HP Framework-specific license terms.

## VContainer

- Upstream project: VContainer by hadashiA
- Bundled location: `ThirdParty/VContainer`
- License declared by the bundled package metadata: MIT
- Upstream documentation: https://vcontainer.hadashikick.jp/

## UniTask

- Upstream project: UniTask by Cysharp, Inc.
- Bundled location: `ThirdParty/UniTask`
- License declared by the bundled package metadata: MIT

## Json.NET / Newtonsoft.Json

- Upstream project: Json.NET by James Newton-King
- Source used for the bundled build: Json.NET 13.0.4 (`netstandard2.0`)
- Bundled assembly: `ThirdParty/NewtonsoftJson/HP.Framework.NewtonsoftJson.dll`
- The assembly name is intentionally changed from `Newtonsoft.Json` to `HP.Framework.NewtonsoftJson` so projects that already use Unity's Newtonsoft package do not collide with HP Framework.
- Namespace remains `Newtonsoft.Json`; `HP.Framework.Extensions` references only the private bundled assembly.
- License: MIT. A copy is included at `ThirdParty/NewtonsoftJson/LICENSE.md`.

## Safe Area Helper

- Bundled location: `ThirdParty/SafeArea`
- Restored from the legacy HP/Base repository because HP Framework retains the Safe Area Helper utility.
- The legacy snapshot does not contain a license file or package metadata identifying redistribution terms.
- Verify the original Safe Area Helper licensing terms before publishing this folder in a public repository or redistributing it outside your permitted scope.

When updating vendored dependencies, preserve their upstream package metadata and license obligations.
