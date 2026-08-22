# Agent Guidelines for NoSleep

This document defines core rules and instructions for AI coding agents working on this codebase.

## 🌐 Language & Localization
- **Code & Comments**: All code, comments, XML docstrings, variable/method/class names, and in-app texts MUST ALWAYS be written in **English**.
- **Documentation & Commits**: All documentation files (markdown), changelogs, commit messages, and PR descriptions must be in **English**.
- **User Conversation**: You may reply to the user in their preferred language (e.g., German if addressed in German), but any code modifications, comments, and files created in the repository must strictly be in **English**.

## 🛠️ Project Context & Architecture
- **Project**: `NoSleep` is a lightweight, standalone, and portable Windows utility (C# / WinForms) designed to prevent system standby during network and disk throughput spikes.
- **Platform**: Windows 10 and Windows 11.
- **Dependencies**: Native Win32 API (`SetThreadExecutionState` via P/Invoke), Performance Counters, and standard .NET libraries. Avoid introducing heavy or unnecessary external dependencies to keep the binary standalone and portable.

## 💻 Coding Conventions & Quality
- **Style**: Follow standard Microsoft C# coding conventions.
- **Comments**: Keep comments clear, concise, and exclusively in English. Explain the rationale (*why*) behind complex logic or Win32 interop rather than merely repeating what the code does.
- **Integrity**: Preserve existing comments, docstrings, and architectural patterns unless explicitly asked to modify or refactor them.
- **Resource Management**: Properly dispose of native resources, Performance Counters, and Windows Forms components (`IDisposable`).

## 🔨 Build & Testing
- Build scripts (`build.bat`, `build.ps1`) compile the standalone executable.
- Ensure all modifications compile cleanly without errors or warnings before completing tasks.
