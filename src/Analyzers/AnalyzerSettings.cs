﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Roslynator
{
    public sealed class AnalyzerSettings : CodeAnalysisSettings
    {
        public static AnalyzerSettings Current { get; } = new AnalyzerSettings();
    }
}
