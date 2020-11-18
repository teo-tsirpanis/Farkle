function Test-ExitCode () {if ($LASTEXITCODE -ne 0) {exit $LASTEXITCODE}}

dotnet tool restore
Test-ExitCode
dotnet paket restore
Test-ExitCode
dotnet tool run fake run ./build.fsx @args
