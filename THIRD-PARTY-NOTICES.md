# Third-party notices

The release binaries of Marbas are self-contained single files: they embed the
.NET runtime and every library below, so their licences travel with them. The
list covers the packages `Marbas.fsproj` ships, direct and transitive; build,
analyzer and test packages are not in the binary and are not listed.

| Component | Licence | Copyright | Source |
|---|---|---|---|
| .NET runtime, `Microsoft.Extensions.*` | MIT | Copyright (c) .NET Foundation and Contributors | https://github.com/dotnet/runtime |
| FSharp.Core | MIT | Copyright (c) Microsoft Corporation. | https://github.com/dotnet/fsharp |
| FsToolkit.ErrorHandling | MIT | Copyright (c) 2018 DemystifyFP | https://github.com/demystifyfp/FsToolkit.ErrorHandling |
| Markdig | BSD-2-Clause | Copyright (c) 2016-2026, Alexandre Mutel | https://github.com/xoofx/markdig |
| SharpCompress | MIT | Copyright (c) 2014 Adam Hathcock | https://github.com/adamhathcock/sharpcompress |
| SharpZipLib | MIT | Copyright © 2000-2018 SharpZipLib Contributors | https://github.com/icsharpcode/SharpZipLib |
| System.IO.Abstractions, TestableIO.System.IO.Abstractions, TestableIO.System.IO.Abstractions.Wrappers | MIT | Copyright (c) Tatham Oddie and Contributors | https://github.com/TestableIO/System.IO.Abstractions |
| Testably.Abstractions.FileSystem.Interface | MIT | Copyright (c) 2022 Valentin Breuß | https://github.com/Testably/Testably.Abstractions |
| Terminal.Gui | MIT | Copyright 2007-2011 Novell Inc; Copyright 2017 Microsoft Corp | https://github.com/tui-cs/Terminal.Gui |
| ColorHelper | MIT | Copyright (c) 2020 Artyom Gritsuk | https://github.com/iamartyom/ColorHelper |
| JetBrains.Annotations | MIT | Copyright (c) 2016-2024 JetBrains s.r.o. | https://github.com/JetBrains/JetBrains.Annotations |
| Onigwrap | MIT | Copyright (c) 2024 Aikawa Yataro | https://github.com/aikawayataro/Onigwrap |
| Oniguruma, linked into the native library of Onigwrap | BSD-2-Clause | Copyright (c) 2002-2021  K.Kosako  <kkosako0@gmail.com> | https://github.com/kkos/oniguruma |
| TextMateSharp, TextMateSharp.Grammars | MIT | Copyright (c) 2021 Daniel Peñalba | https://github.com/danipen/TextMateSharp |
| Wcwidth | MIT | Copyright Patrik Svensson. Phil Scott | https://github.com/spectreconsole/wcwidth |

TextMateSharp.Grammars also carries TextMate grammars and colour themes taken
from other projects, each under its own licence; the `cgmanifest.json` beside
each grammar in
[its resources](https://github.com/danipen/TextMateSharp/tree/master/src/TextMateSharp.Grammars/Resources/Grammars)
names the project and the licence it comes from.

## MIT License

Applies to every component above marked MIT, with the copyright notice of that
component in its row.

```
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Markdig

```
Copyright (c) 2016-2026, Alexandre Mutel
All rights reserved.

Redistribution and use in source and binary forms, with or without modification
, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

## Oniguruma

```
Copyright (c) 2002-2021  K.Kosako  <kkosako0@gmail.com>
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:
1. Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE AUTHOR AND CONTRIBUTORS ``AS IS'' AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED.  IN NO EVENT SHALL THE AUTHOR OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS
OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY
OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF
SUCH DAMAGE.
```
