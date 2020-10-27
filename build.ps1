dotnet tool restore
if ($LASTEXITCODE -ne 0) {exit $LASTEXITCODE}
dotnet tool run fake run ./build.fsx @args
