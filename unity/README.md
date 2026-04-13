# Unity Prototype Line

This branch carries a separate Unity-first prototype line under [unity/OrangeDot.Supabase.Unity](/home/dev/orange-dot-supabase-sdk/unity/OrangeDot.Supabase.Unity/README.md).

Current shape:

- `unity/Vendor/BirdMessenger` -> Unity package wrapper for resumable-upload dependency
- `unity/Vendor/MimeMapping` -> Unity package wrapper for MIME inference dependency
- `modules/core-csharp/Core` -> Unity package root for shared portable core types
- `modules/gotrue-csharp/Gotrue` -> Unity package root for auth
- `modules/postgrest-csharp/Postgrest` -> Unity package root for data
- `modules/functions-csharp/Functions` -> Unity package root for Edge Functions
- `modules/storage-csharp/Storage` -> Unity package root for storage
- `unity/OrangeDot.Supabase.Unity` -> Unity-facing composition package and sample

The current sample scene stays centered on auth + data, with an optional Edge Function call path if you deploy the example function from the package README. The runtime package now also exposes storage composition, but that path is not in the first sample scene yet.

The root `src/OrangeDot.Supabase` package remains the server-side line. The Unity work on this branch composes child modules directly instead of trying to reuse hosted startup or DI-oriented code from the root package.

## References

- [Unity package README](/home/dev/orange-dot-supabase-sdk/unity/OrangeDot.Supabase.Unity/README.md)
- [General Unity concepts](/home/dev/orange-dot-supabase-sdk/unity/references/unity-concepts.md)
- [Local Unity skill](/home/dev/orange-dot-supabase-sdk/.agents/skills/unity-first-sdk/SKILL.md)
