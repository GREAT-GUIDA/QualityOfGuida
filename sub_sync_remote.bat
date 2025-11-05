@echo off
setlocal
set "SUBMODULE_DIR=GuidaSharedCode"

echo =============================================
echo =  Submodule Sync (Remote Only)
echo =============================================
echo.

REM --- 1. 更新子模块到远程最新版 ---
echo [1/2] Updating '%SUBMODULE_DIR%' to remote HEAD...
git submodule update --remote %SUBMODULE_DIR%
echo.

REM --- 2. 在父仓库中提交更新 ---
echo [2/2] Staging and committing in Parent Repository...
git add %SUBMODULE_DIR%
git commit -m "chore: Sync %SUBMODULE_DIR% to remote"

REM (Note: This commit will fail if already up-to-date, which is fine)
IF %ERRORLEVEL% NEQ 0 (
    echo (Note: Parent repo already up-to-date or nothing to commit)
) ELSE (
    echo Parent commit successful.
)
echo.

echo =============================================
echo Sync Complete.
echo =============================================
echo.

:end
endlocal
pause