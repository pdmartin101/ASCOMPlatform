set current=%CD%
echo -
echo %1
cd %current%\%1\bin
rd /s /Q Release
mklink /D Release %2\%3
cd %current%
