# Trelnex.Core.Data.Emulator

## Overview

`Trelnex.Core.Data.Emulator` is a .NET library designed to assist development and testing of data access.

## InMemoryCommandProvider Class

The `InMemoryCommandProvider<TInterface, TItem>` class is an implementation of the  `ICommandProvider<TInterface, TItem>` interface. It provides methods for creating, reading, updating, deleting, and querying items in an in-memory store, with optional persistence to local storage.

The developer may instantiate an instance of `InMemoryCommandProvider` to assist development and testing of their business logic.
