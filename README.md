# Directory Synchronization

A .NET 10 console application that periodically mirrors a source directory to a destination directory. It recursively copies new files, updates changed files, and deletes destination files and directories that no longer exist in the source.

> **Warning:** deletion is part of the synchronization behavior. Anything present only in the destination can be deleted. Back up important data and verify the source and destination paths before running the application.

## Requirements

- .NET 10 SDK to build and run locally
- Docker with the Compose plugin to run in a container

## Configuration

The default settings are in [`Synchronization/appsettings.json`](Synchronization/appsettings.json):

```json
{
  "Folders": {
    "SourceFolder": "srcFolder",
    "DestinationFolder": "destFolder",
    "LogFilePath": "Logs"
  },
  "Synchronization": {
    "DryRun": false,
    "AllowEmptySource": false,
    "SourceReadyFile": ".sync-ready",
    "IntervalSeconds": 15
  }
}
```

Paths in JSON are resolved relative to the application output directory. Configuration is loaded in this order:

1. `appsettings.json`
2. `appsettings.{DOTNET_ENVIRONMENT}.json`, when present
3. Environment variables

Environment variables take precedence. Use double underscores (`__`) for nested settings:

| Setting | Environment variable |
| --- | --- |
| `Folders:SourceFolder` | `Folders__SourceFolder` |
| `Folders:DestinationFolder` | `Folders__DestinationFolder` |
| `Folders:LogFilePath` | `Folders__LogFilePath` |
| `Synchronization:DryRun` | `Synchronization__DryRun` |
| `Synchronization:AllowEmptySource` | `Synchronization__AllowEmptySource` |
| `Synchronization:SourceReadyFile` | `Synchronization__SourceReadyFile` |
| `Synchronization:IntervalSeconds` | `Synchronization__IntervalSeconds` |

Before starting the application, make sure the source directory exists and is readable. The application creates the destination and log directories when needed, so it must have permission to write to their parent directories.

The source and destination paths are normalized before use. They must refer to different directories, and neither may be inside the other; this prevents recursive copying and accidental deletion of source data. Synchronization also removes destination files that are not present in the source, so choose the destination carefully and back up any data it contains.

`Synchronization:DryRun` defaults to `false`. Set it to `true` to log the planned directory creation, file copies/updates, and deletions without changing the destination. Review the console output before running with dry-run disabled.

Before synchronization can delete destination data, the source must contain the configured ready file (default: `.sync-ready`). Create this file only after the source has been fully mounted and populated. The marker is not copied to the destination. This guards against unmounted or partially populated sources; the process that populates the source must create the marker only when the source is complete.

Even with the ready marker, an empty source is blocked by default. If an empty source is intentional and should clear the destination, set `Synchronization:AllowEmptySource` to `true`; the ready marker is still required.

`Synchronization:IntervalSeconds` controls the recurring interval and defaults to 15 seconds. Valid values are from 1 through 86400.

## Run locally

From the repository root, set paths explicitly for your environment:

```bash
Folders__SourceFolder="$PWD/Synchronization/srcFolder" \
Folders__DestinationFolder="$PWD/Synchronization/destFolder" \
Folders__LogFilePath="$PWD/Synchronization/Logs" \
Synchronization__DryRun=true \
dotnet run --project Synchronization/Synchronization.csproj
```

Remove `Synchronization__DryRun=true` (or set it to `false`) to apply changes. To intentionally mirror an empty source, create the ready marker and set `Synchronization__AllowEmptySource=true`.

The first synchronization starts immediately and then runs at the configured interval. Press `Ctrl+C` to stop the application; it waits for an active job to finish before shutting down.

## Run with Docker Compose

Build and start the service:

```bash
docker compose up --build
```

Compose configures the service to use `/var/tmp/srcFolder`, `/var/tmp/destFolder`, and `/var/tmp/logs`, backed by named volumes (`src`, `dest`, and `logs`). The volumes persist across container restarts. Stop the service with `Ctrl+C` or `docker compose down`.

The source volume must contain at least one data file and the `.sync-ready` marker before the application starts a synchronization. Populate the source volume first, then create the marker only after it is complete. For example:

```bash
docker compose run --rm --no-deps --entrypoint /bin/sh synchronization -c 'touch /var/tmp/srcFolder/.sync-ready'
```

When modifying a ready source, remove the marker before changing its contents and recreate it after the update is complete. The marker is not copied to the destination.

To remove the persisted data as well as stop the service, use `docker compose down --volumes`. **This permanently deletes the Compose-managed source, destination, and log volumes.**

## Synchronization behavior

- Recursively copies new files and creates missing directories.
- Compares existing files by SHA-256 and atomically replaces changed files.
- Deletes destination files and directories that are absent from the source.
- Skips symbolic links and reparse points; destination paths containing them are rejected.
- Rejects equal or nested source/destination paths.
- Retries recognized transient I/O failures up to three attempts, with one- and two-second delays between attempts; permanent errors are not retried.
- Prevents overlapping executions of the scheduled job.
- Supports a dry-run mode and requires a ready marker before deleting destination data.
- Detects source-file changes during a copy and refuses to replace the destination with an inconsistent snapshot.
- Allows the recurring interval to be configured.

Atomic replacement protects individual files from being left partially written. A complete synchronization is **not transactional**: if a later operation fails, earlier file operations may already have taken effect.

### Recovery after an interrupted run

The application uses a replay-based recovery strategy rather than rolling back already applied file operations:

1. Before changing files, it atomically writes `.synchronization.pending.json` to the configured log directory.
2. It updates and copies source files before deleting destination-only files, so an interruption before the delete phase preserves extra destination data rather than removing it early.
3. If an operation fails or the process stops, the journal remains. The next run requires the source-ready marker and replays the full reconciliation from the current source; file copies and updates are idempotent.
4. The journal is removed only after all synchronization operations complete successfully.

If the source changed after an interrupted run, the current ready source is authoritative. The journal records the previous source and destination paths and start time for diagnosis; it does not save file contents or provide rollback. Preserve the journal and inspect logs if recovery keeps failing.

## Logging and metrics

Application logs are written to the console. File-operation entries are appended to `SynchronizationLog.txt` in the configured log directory. Logging failures are surfaced rather than silently ignored.

The application defines the `Synchronization` `System.Diagnostics.Metrics` meter with these instruments:

- `synchronization.files.copied`
- `synchronization.files.updated`
- `synchronization.files.deleted`
- `synchronization.directories.deleted`
- `synchronization.jobs.completed`
- `synchronization.jobs.failed`
- `synchronization.job.duration` (milliseconds)

The meter is available for in-process .NET diagnostics or an instrumentation/export pipeline. To inspect it live, install the diagnostics tool and monitor the application process:

```bash
dotnet tool install --global dotnet-counters
dotnet-counters monitor --counters Synchronization --process-id <PID>
```

Replace `<PID>` with the running application's process ID. This project does not configure a remote metrics exporter or dashboard.

## Build and test

```bash
dotnet build Synchronization/Synchronization.csproj -c Release
dotnet test SynchronizationTests/SynchronizationTests.csproj
dotnet publish Synchronization/Synchronization.csproj -c Release
```

Tests use temporary directories and do not require the configured synchronization folders.
