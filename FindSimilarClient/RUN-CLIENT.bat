@echo off

cd /D "C:\Users\perner\My Projects\FindSimilarCore\FindSimilarClient\bin\Release\netcoreapp3.1\win-x64\publish"
start "" FindSimilarClient FingerprintDatabase="C:\Users\perner\My Projects\fingerprint.db"

"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe" --app="https://localhost:5001"
