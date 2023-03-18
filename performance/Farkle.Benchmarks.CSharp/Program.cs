// Copyright © Theodore Tsirpanis and Contributors.
// SPDX-License-Identifier: MIT

using BenchmarkDotNet.Running;
using System.Reflection;

BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args);
