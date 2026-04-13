# BirdMessenger Unity Wrapper

This package wraps the upstream `BirdMessenger` `3.1.4` `netstandard2.0` assembly for the Unity-first Supabase prototype line in this repo.

Source project:

- https://github.com/bluetianx/BirdMessenger

This wrapper does not change the library API. It exists so local Unity package composition can resolve the resumable-upload dependency used by `Supabase.Storage`.
