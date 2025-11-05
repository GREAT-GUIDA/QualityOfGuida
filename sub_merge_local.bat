@echo off
setlocal
set "SUBMODULE_DIR=GuidaSharedCode"

echo =============================================
echo =  Submodule Merge (Local + Remote)
echo =  (Hardcoded Commit Messages)
echo =============================================
echo.

REM --- 1. 提交子模块的本地更改 ---
echo [1/3] Committing changes in '%SUBMODULE_DIR%'...
cd %SUBMODULE_DIR%
IF %ERRORLEVEL% NEQ 0 ( goto :error_path )

git add .
git commit -m "WIP: Local changes before sync"
echo.

REM --- 2. 在子模块中拉取和变基 ---
echo [2/3] Pulling & Rebasing remote changes...
git pull --rebase

IF %ERRORLEVEL% NEQ 0 (
    echo.
    echo *******************************************************
    echo * *
    echo * ERROR: 'git pull --rebase' FAILED!                *
    echo * LIKELY A MERGE CONFLICT.                            *
    echo * *
    echo * ACTION:                                             *
    echo * 1. Open Git Bash/VS Code in '%SUBMODULE_DIR%'.      *
    echo * 2. Resolve conflicts and run 'git rebase --continue'. *
    echo * *
    echo *******************************************************
    echo.
    echo == After resolving, press any key to continue... ==
    pause > nul
) ELSE (
    echo Rebase successful.
)
echo.

REM --- 3. 在父仓库中提交更新 ---
echo [3/3] Committing update in Parent Repository...
cd ..
git add %SUBMODULE_DIR%
git commit -m "chore: Update %SUBMODULE_DIR% with local merge"
echo.

echo =============================================
echo Workflow Complete.
echo =============================================
goto :end

:error_path
echo ERROR: Could not find directory '%SUBMODULE_DIR%'.
echo.

:end
endlocal
pause