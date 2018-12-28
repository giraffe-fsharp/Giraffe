#!/bin/sh
FrameworkPathOverride=$(dirname "$(which mono)")/../lib/mono/4.5/
export FrameworkPathOverride
pwsh ./build.ps1
