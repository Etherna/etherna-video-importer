# Etherna Video Importer

## Overview
Tool to import videos on Etherna from different sources.

## Instructions
Download and extract binaries from [release page](https://github.com/Etherna/etherna-video-importer/releases).

Currently exists two versions:
* `EthernaVideoImporter` for a generic use
* `EthernaVideoImporter.Devcon` to import specifically Devcon Archive's videos

Etherna Video Importer requires at least [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) and [ASP.NET Core 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) installed on local machine to run, or it needs the `selfcontained` version of package, that already contains framework dependencies.

### Setup FFmpeg and FFprobe
To run the importer you need to download [FFmpeg and FFprobe](https://ffmpeg.org/download.html) locally, and copy them into the default folder "\FFmpeg", or specify its location with arguments.

### How to use

**EthernaVideoImporter's help**
```
Usage:  evi COMMAND SOURCE_URI [SOURCE_URI, ...] [OPTIONS]

Commands:
  json      Import from json video list (requires metadata descriptor, see below)
  youtube   Import from multiple YouTube links. Supports videos, channels and playlists urls

General Options:
  -k, --api-key           Api Key (optional)
  -f, --ffmpeg-path       Path to FFmpeg folder (default: search to <app_dir>/FFmpeg or global install)
  -i, --ignore-update     Ignore new version of EthernaVideoImporter
  -a, --auto-purchase     Accept automatically purchase of all batches
  -w, --write-file        Write published videos result to a JSON file

Video Management Options:
  -t, --ttl               TTL (days) Postage Stamp (default: 365 days)
  -o, --offer             Offer video downloads to everyone
  --no-pin                Don't pin videos (pinning by default)
  --force                 Force upload video if they already have been uploaded
  -m, --remove-missing    Remove indexed videos generated with this tool but missing from source
  --remove-unrecognized   Remove indexed videos not generated with this tool
  -u, --unpin             Try to unpin contents removed from index

Bee Node Options:
  --bee-node              Use bee native node
  --bee-url               URL of Bee node (default: http://localhost/)
  --bee-api-port          Port used by API (default: 1633)
  --bee-debug-port        Port used by Debug (default: 1635)

Json videos metadata format:
To import from a video list you need a metadata descriptor file. Metadata is a JSON file with the following structure:

[
    {
        "Id": "myId1",
        "Title": "First video title",
        "Description": "My first video description",
        "VideoFilePath": "path/to/your/video1.mp4",
        "ThumbnailFilePath": "path/to/your/optional/thumbnail1.jpg",
        "OldIds": [
            "optionalOldId1",
            "optionalOldId2"
        ]
    },
    {
        "Id": "myId2",
        "Title": "Second video title",
        "Description": "My second video description",
        "VideoFilePath": "http://example.com/stream.m3u8",
        "ThumbnailFilePath": "path/to/your/optional/thumbnail2.jpg"
    },
    ...
]

Id field is mandatory, and is needed to trace same video through different executions. Each Id needs to be unique.

Run 'evi -h' or 'evi --help' to print help.
```

**EthernaVideoImporter.Devcon's help**
```
Usage:  evid MD_FOLDER [OPTIONS]

General Options:
  -k, --api-key           Api Key (optional)
  -f, --ffmpeg-path       Path to FFmpeg folder (default: search to <app_dir>/FFmpeg or global install)
  -i, --ignore-update     Ignore new version of EthernaVideoImporter
  -a, --auto-purchase     Accept automatically purchase of all batches

Video Management Options:
  -t, --ttl               TTL (days) Postage Stamp (default: 365 days)
  -o, --offer             Offer video downloads to everyone
  --no-pin                Don't pin videos (pinning by default)
  --force                 Force upload video if they already have been uploaded
  -m, --remove-missing    Remove indexed videos generated with this tool but missing from source
  --remove-unrecognized   Remove indexed videos not generated with this tool
  -u, --unpin             Try to unpin contents removed from index

Bee Node Options:
  --bee-node              Use bee native node
  --bee-url               URL of Bee node (default: http://localhost/)
  --bee-api-port          Port used by API (default: 1633)
  --bee-debug-port        Port used by Debug (default: 1635)

Run 'evid -h' or 'evid --help' to print help.
```

# Issue reports
If you've discovered a bug, or have an idea for a new feature, please report it to our issue manager based on Jira https://etherna.atlassian.net/projects/EVI.

Detailed reports with stack traces, actual and expected behaviours are welcome.

# Questions? Problems?
For questions or problems please write an email to [info@etherna.io](mailto:info@etherna.io).
