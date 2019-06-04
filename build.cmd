@echo off

set toolpath=".\.fake"

where /Q fake

if %ERRORLEVEL% neq 0 (

    if not exist %toolpath%\fake (
        echo FAKE does not exist. Installing...
        if exist %toolpath% (
            rmdir /S /Q %toolpath%
        )
        dotnet tool install fake-cli --tool-path %toolpath%
    )

    %toolpath%\fake run .\build.fsx %*
) else fake run .\build.fsx %*
