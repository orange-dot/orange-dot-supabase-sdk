# MimeMapping Unity Wrapper

This package wraps the upstream `MimeMapping` `3.0.1` `netstandard2.0` assembly for the Unity-first Supabase prototype line in this repo.

Source project:

- https://github.com/zone117x/MimeMapping

This wrapper does not change the library API. It exists so local Unity package composition can resolve MIME inference used by `Supabase.Storage`.
