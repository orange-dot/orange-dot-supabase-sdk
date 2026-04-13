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

The current sample scene covers auth + data, with optional Edge Function and authenticated Storage flows from the same controller. The runtime package composes storage directly and the sample now demonstrates upload, list, and signed URL creation against a user-scoped path inside a pre-created bucket.

The root `src/OrangeDot.Supabase` package remains the server-side line. The Unity work on this branch composes child modules directly instead of trying to reuse hosted startup or DI-oriented code from the root package.

## References

- [Unity package README](/home/dev/orange-dot-supabase-sdk/unity/OrangeDot.Supabase.Unity/README.md)
- [General Unity concepts](/home/dev/orange-dot-supabase-sdk/unity/references/unity-concepts.md)
- [Local Unity skill](/home/dev/orange-dot-supabase-sdk/.agents/skills/unity-first-sdk/SKILL.md)
