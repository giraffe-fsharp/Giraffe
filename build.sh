#!/usr/bin/env bash

set -eu

export FrameworkPathOverride=$(dirname $(which mono))/../lib/mono/4.5/

dotnet restore src/Giraffe
dotnet build src/Giraffe

dotnet restore tests/Giraffe.Tests
dotnet build tests/Giraffe.Tests
# Ignoring net4x because dotnet test doesn't support mono yet https://github.com/Microsoft/vstest/issues/679
dotnet test -f netcoreapp1.1 tests/Giraffe.Tests/Giraffe.Tests.fsproj

dotnet restore samples/SampleApp/SampleApp
dotnet build samples/SampleApp/SampleApp

dotnet restore samples/SampleApp/SampleApp.Tests
dotnet build samples/SampleApp/SampleApp.Tests
dotnet test -f netcoreapp1.1 samples/SampleApp/SampleApp.Tests/SampleApp.Tests.fsproj

