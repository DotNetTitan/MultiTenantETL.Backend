# ⚠️ IMPORTANT: Pull Request Target Branch

**This PR should target the `develop` branch, NOT `main`**

## Action Required

When creating or reviewing this pull request on GitHub:

1. Go to the PR page
2. Click "Edit" next to the title (or when creating the PR)
3. Change the **base branch** from `main` to `develop`
4. Save the changes

## Why develop?

This PR contains new feature development (SignalR real-time log streaming) that should be integrated into the development branch first, following the GitFlow workflow, before being merged to main in a release.

## Current Status

- **Current Branch**: `copilot/stream-logs-with-signalr`  
- **Should Target**: `develop` branch
- **Contains**: Real-time pipeline execution log streaming via SignalR

## For Maintainers

If this PR is accidentally created against `main`, please change the base branch to `develop` before merging.

---

*This file can be deleted after the PR is correctly configured to target develop.*
