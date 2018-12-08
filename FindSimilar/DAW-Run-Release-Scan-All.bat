@echo off

cd /D "D:\Development Projects\Own Projects\FindSimilarCore\FindSimilar\bin\Release\netcoreapp2.1\win-x64\publish"
FindSimilar scan "H:\SAMPLES\ACID LOOPS & SAMPLES" ^
-l "D:\Development Projects\Own Projects\findsimilar.log" ^
-e "D:\Development Projects\Own Projects\findsimilar_error.log" ^
-d "D:\Development Projects\Own Projects\fingerprint.db"

pause

@echo off
