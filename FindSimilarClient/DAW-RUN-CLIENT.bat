@echo off

cd /D "D:\Development Projects\Own Projects\FindSimilarCore\FindSimilarClient\bin\Release\netcoreapp2.1\win-x64\publish"
start "" FindSimilarClient FingerprintDatabase="D:\Development Projects\Own Projects\fingerprint.db"

"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe" --app="https://localhost:5001"
