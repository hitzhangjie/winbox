SLN    := WinBox.sln
HOST   := src/WinBox.Host
CONFIG ?= Debug
RUNTIME ?= win-x64
VERSION ?=

.DEFAULT_GOAL := help

.PHONY: help restore build test run clean rebuild ci dist fmt

help:
	@echo "WinBox make targets:"
	@echo "  make restore   - restore NuGet packages"
	@echo "  make build     - build solution (CONFIG=$(CONFIG))"
	@echo "  make test      - run all tests"
	@echo "  make run       - run host (Shift+Alt+U launcher)"
	@echo "  make clean     - clean build outputs"
	@echo "  make rebuild   - clean + build"
	@echo "  make ci        - Release restore/build/test (like CI)"
	@echo "  make dist      - publish self-contained win-x64 zip (Windows 11 amd64)"
	@echo ""
	@echo "Examples:"
	@echo "  make build CONFIG=Release"
	@echo "  make test"
	@echo "  make run"
	@echo "  make dist"
	@echo "  make dist VERSION=0.1.0"

restore:
	dotnet restore $(SLN)

build:
	dotnet build $(SLN) -c $(CONFIG)

test:
	dotnet test $(SLN) -c $(CONFIG) --verbosity minimal

run: build
	dotnet run --project $(HOST) -c $(CONFIG) --no-launch-profile

clean:
	dotnet clean $(SLN) -c $(CONFIG)
	dotnet clean $(SLN) -c Release
	powershell -NoProfile -Command "if (Test-Path -LiteralPath 'artifacts') { Remove-Item -LiteralPath 'artifacts' -Recurse -Force }"

rebuild: clean build

ci:
	dotnet restore $(SLN)
	dotnet build $(SLN) -c Release --no-restore
	dotnet test $(SLN) -c Release --no-build --verbosity minimal

# Portable package for Windows 11 amd64. VERSION optional (tag / Directory.Build.props / 0.0.0-dev).
dist:
	powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dist.ps1 -Configuration Release -Runtime "$(RUNTIME)" $(if $(VERSION),-Version "$(VERSION)",)
