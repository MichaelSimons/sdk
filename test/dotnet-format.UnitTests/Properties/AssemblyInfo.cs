// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

// Tests mutate Environment.CurrentDirectory (process-global shared state) which causes
// intermittent failures when run in parallel. See https://github.com/dotnet/sdk/issues/54249
[assembly: CollectionBehavior(DisableTestParallelization = true)]
