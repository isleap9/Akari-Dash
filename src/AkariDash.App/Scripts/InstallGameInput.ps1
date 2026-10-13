# Microsoft GameInput: install. AkariOS-Ultimate never reinstalls GameInput on its own, so this is
# Akari-Dash's: Microsoft's own package, from winget.

winget install --id Microsoft.GameInput --source winget --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity
