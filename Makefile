SLN    := WinBox.sln
HOST   := src/WinBox.Host
CONFIG ?= Debug

.DEFAULT_GOAL := help

.PHONY: help restore build test run clean rebuild ci fmt

help:
	@echo "WinBox make targets:"
	@echo "  make restore   - restore NuGet packages"
	@echo "  make build     - build solution (CONFIG=$(CONFIG))"
	@echo "  make test      - run all tests"
	@echo "  make run       - run host console demo"
	@echo "  make clean     - clean build outputs"
	@echo "  make rebuild   - clean + build"
	@echo "  make ci        - Release restore/build/test (like CI)"
	@echo ""
	@echo "Examples:"
	@echo "  make build CONFIG=Release"
	@echo "  make test"
	@echo "  make run"
restore:
	dotnet restore $(SLN)

build:
	dotnet build $(SLN) -c $(CONFIG)

test:
	dotnet test $(SLN) -c $(CONFIG) --verbosity minimal

run:
	dotnet run --project $(HOST) -c $(CONFIG) --no-launch-profile

clean:
	dotnet clean $(SLN) -c $(CONFIG)
	dotnet clean $(SLN) -c Release

rebuild: clean build

ci:
	dotnet restore $(SLN)
	dotnet build $(SLN) -c Release --no-restore
	dotnet test $(SLN) -c Release --no-build --verbosity minimal
