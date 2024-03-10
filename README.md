# Synchronization

### Task Structure
```sh
├── README.md
├── Synchronization
│   ├── Exception
│   │   └── FilePathException.cs
│   ├── Extensions
│   │   └── OperationExtensions.cs
│   ├── Job
│   │   └── SynchronizationJob.cs
│   ├── Logs
│   │   ├── ConsoleLog.cs
│   │   └── SynchronizationLog.txt
│   ├── Operations
│   │   ├── Copy.cs
│   │   ├── Delete.cs
│   │   └── Update.cs
│   ├── Program.cs
│   ├── Settings
│   │   ├── FolderOptions.cs
│   │   └── appsettings.json
│   ├── Synchronization.csproj
│   ├── Utils
│   │   ├── CheckSumUtils.cs
│   │   └── WriteLogFileUtils.cs│   
└── Synchronization.sln
```
![](https://img.shields.io/badge/build-success-brightgreen.svg)
[![License](http://img.shields.io/:license-apache-blue.svg)](http://www.apache.org/licenses/LICENSE-2.0.html)

## Instructions

This solution not only relies on editing four paths to be compiled succesfully but it's responsible for synchronizing two folders. Therefore, there are two files which must be updated according to the source and target folders as well as the settings as below.

- The file **program.cs** stores the kick off path value which is expected to reflect the current folder path: 
```csharp
string _settingsPath = @"/foo/Synchronization/Synchronization/Settings/appsettings.json";
```

- On the other hand, the file **appsettings.json** stores the paths related to the two synchronized folders as well as the log target file that must reflect the current folder path: 
```json
{
  "Folders": {
    "SourceFolder": "/foo/Synchronization/Synchronization/srcFolder/",
    "DestinationFolder": "foo/Synchronization/Synchronization/destFolder/",
    "LogFilePath": "foo/Synchronization/Synchronization/Logs/"
  }
}
```

- The file **SynchronizationLog.txt** has the operation logs.

## Additional Info
- The task was developed by using the folders srcFolder and destFolder added to the project's root path.
- It is highly recommended keeping the files closed before any job execution.
