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
Usage:  EthernaVideoImporter SOURCE_TYPE SOURCE_URI [OPTIONS]

Source types:
  ytchannel     YouTube channel
  ytvideo       YouTube video

Options:
  -ff   Path FFmpeg (default dir: .\FFmpeg\)
  -t    TTL (days) Postage Stamp (default value: 365 days)
  -o    Offer video downloads to everyone
  -p    Pin videos
  -m    Remove indexed videos generated with this tool but missing from source
  -e    Remove indexed videos not generated with this tool
  -u    Try to unpin contents removed from index
  -i    Ignore new version of EthernaVideoImporter

Run 'EthernaVideoImporter -h' to print help
```

**EthernaVideoImporter.Devcon's help**
```
Usage:  EthernaVideoImporter.Devcon md MD_FOLDER [OPTIONS]

Options:
  -ff   Path FFmpeg (default dir: .\FFmpeg\)
  -t    TTL (days) Postage Stamp (default value: 365 days)
  -o    Offer video downloads to everyone
  -p    Pin videos
  -m    Remove indexed videos generated with this tool but missing from source
  -e    Remove indexed videos not generated with this tool
  -u    Try to unpin contents removed from index
  -i    Ignore new version of EthernaVideoImporter.Devcon

Run 'EthernaVideoImporter.Devcon -h' to print help
```

# Issue reports
If you've discovered a bug, or have an idea for a new feature, please report it to our issue manager based on Jira https://etherna.atlassian.net/projects/EVI.

Detailed reports with stack traces, actual and expected behaviours are welcome.

# Questions? Problems?
For questions or problems please write an email to [info@etherna.io](mailto:info@etherna.io).
