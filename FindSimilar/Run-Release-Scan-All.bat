@echo off

cd "C:\Users\perner\My Projects\FindSimilarCore\FindSimilar\bin\Release\netcoreapp2.2\win-x64\publish"

FindSimilar scan "C:\Users\perner\Amazon Drive\Documents\Audio\FL Projects" ^
-l "C:\Users\perner\My Projects\findsimilar.log" ^
-e "C:\Users\perner\My Projects\findsimilar_error.log" ^
-d "C:\Users\perner\My Projects\fingerprint.db"

pause