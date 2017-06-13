#!/usr/bin/env bash

set -eu

export FrameworkPathOverride=$(dirname $(which mono))/../lib/mono/4.5/

dotnet restore src/Giraffe
dotnet build src/Giraffe

dotnet restore tests/Giraffe.Tests
dotnet build tests/Giraffe.Tests

pushd tests/Giraffe.Tests
dotnet xunit 
popd

dotnet restore samples/SampleApp/SampleApp
dotnet build samples/SampleApp/SampleApp

dotnet restore samples/SampleApp/SampleApp.Tests
dotnet build samples/SampleApp/SampleApp.Tests
pushd samples/SampleApp/SampleApp.Tests/
dotnet xunit 
popd

