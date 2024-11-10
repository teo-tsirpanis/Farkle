function Test-ExitCode () {if ($LASTEXITCODE -ne 0) {exit $LASTEXITCODE}}

dotnet tool restore
Test-ExitCode
dotnet run --project eng/Farkle.Build.fsproj -- @args
