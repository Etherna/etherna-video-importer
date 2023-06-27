# Etherna Video Importer

## Overview
Tool to import videos on Etherna from different sources.

## Instructions
Download and extract binaries from [release page](https://github.com/Etherna/etherna-video-importer/releases).

Currently exists two versions:
* `EthernaVideoImporter` for a generic use
* `EthernaVideoImporter.Devcon` to import specifically Devcon Archive's videos

Etherna Video Importer requires at least [.NET 7 Runtime](https://dotnet.microsoft.com/download/dotnet/7.0) and [ASP.NET Core 7 Runtime](https://dotnet.microsoft.com/download/dotnet/7.0) installed on local machine to run, or it needs the `selfcontained` version of package, that already contains framework dependencies.

**Warning**: this client still requires to be able to open a browser to provide authentication.  
Future releases will improve this aspect accepting API keys instead.

### Setup FFmpeg
To run the importer it is necessary to download [FFmpeg](https://ffmpeg.org/download.html) locally, and copy the binary file into the default folder "\FFmpeg", or specify its location with arguments.

### How to use

**EthernaVideoImporter's help**
```
Usage:  evi COMMAND [OPTIONS] SOURCE_URI

Commands:
  youtube-channel   Import from a YouTube channel
  youtube-video     Import from a YouTube video
  local             Import from local videos (requires metadata descriptor, see below)

General Options:
  -k, --api-key           Api Key (optional)
  -f, --ffmpeg-path       Path to FFmpeg folder (default: ./FFmpeg)
  -i, --ignore-update     Ignore new version of EthernaVideoImporter
  -a, --auto-purchase     Accept automatically purchase of all batches

Video Management Options:
  -t, --ttl               TTL (days) Postage Stamp (default: 365 days)
  -o, --offer             Offer video downloads to everyone
  -p, --pin               Pin videos
  --force                 Force upload video if they already have been uploaded
  -m, --remove-missing    Remove indexed videos generated with this tool but missing from source
  --remove-unrecognized   Remove indexed videos not generated with this tool
  -u, --unpin             Try to unpin contents removed from index

Bee Node Options:
  --bee-node              Use bee native node
  --bee-url               URL of Bee node (default: http://localhost/)
  --bee-api-port          Port used by API (default: 1633)
  --bee-debug-port        Port used by Debug (default: 1635)

Local video metadata format:
To import from local videos you will need a metadata descriptor file. Metadata is a JSON file with the following structure:

[
    {
        "Id": "myId1",
        "Title": "My video1 title",
        "Description": "My video description",
        "VideoFilePath": "path/to/your/video1.mp4",
        "ThumbnailFilePath": "path/to/your/optional/thumbnail1.jpg"
    },
    {
        "Id": "myId2",
        "Title": "My video2 title",
        ...
    },
    ...
]

Paths can be either relative or absolute. ThumbnailFilePath is optional.

Run 'evi -h' or 'evi --help' to print help.
```

**EthernaVideoImporter.Devcon's help**
```
Usage:  evid [OPTIONS] MD_FOLDER

General Options:
  -k, --api-key           Api Key (optional)
  -f, --ffmpeg-path       Path to FFmpeg folder (default: ./FFmpeg)
  -i, --ignore-update     Ignore new version of EthernaVideoImporter
  -a, --auto-purchase     Accept automatically purchase of all batches

Video Management Options:
  -t, --ttl               TTL (days) Postage Stamp (default: 365 days)
  -o, --offer             Offer video downloads to everyone
  -p, --pin               Pin videos
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

#### Local videos
To import from local videos you will need a JSON metadata descriptor file.

The `Id` field is mandatory, and is needed to trace same video through different executions. Each Id needs to be unique in the file.  
The `ThumbnailFilePath` field is optional.

The Json file path needs to be passed as source uri with the source type `local`.

# Issue reports
If you've discovered a bug, or have an idea for a new feature, please report it to our issue manager based on Jira https://etherna.atlassian.net/projects/EVI.

Detailed reports with stack traces, actual and expected behaviours are welcome.

# Questions? Problems?
For questions or problems please write an email to [info@etherna.io](mailto:info@etherna.io).
