"""Writes THIRD-PARTY-NOTICES.txt for one release build of ytplayer (native AOT, Windows).

    python tools/third-party-notices.py <rid> <out file>     e.g. win-x64 publish/THIRD-PARTY-NOTICES.txt

Run it after `dotnet publish -r <rid>`: the package set is read from what that restore resolved for the
RID (src/YtPlayer/obj/project.assets.json), not from Directory.Packages.props, so it names exactly what
went into that build. A package counts when it gave the build a runtime library (compiled into the exe
by native AOT) or a native file (shipped beside it); build tools such as ILLink do not.

Most packages carry their license (a bundled file, or an SPDX expression and a copyright in the
nuspec). The native files do not: SDL3-CS.Native and SdlVulkan.Renderer.WebView.Native are licensed for
their own packaging, while SDL3.dll is SDL's and WebView2Loader.dll is Microsoft's. Their texts are kept
verbatim in tools/notices/ (see NATIVE), and a package that ships a native file missing from NATIVE fails
the script rather than ship it unattributed.
"""
import json
import os
import sys
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, 'src', 'YtPlayer', 'obj', 'project.assets.json')
TFM = 'net10.0-windows'

# Native files shipped beside the exe, by the package that brings them: what the file is, its license,
# and the verbatim text in tools/notices/ (where each text was taken from is noted against it).
NATIVE = {
    # libsdl-org/SDL, LICENSE.txt on main (SDL3-CS.Native 3.5.0's SDL3.dll says "Copyright (C) 2026 Sam
    # Lantinga" and SDL 3.5.0 had no release tag yet).
    'SDL3-CS.Native': ('SDL3.dll, Simple DirectMedia Layer', 'zlib', 'Copyright (C) 1997-2026 Sam Lantinga',
                       'https://libsdl.org', 'SDL3.txt'),
    # LICENSE.txt of the Microsoft.Web.WebView2 package (1.0.3719.77, the loader's version), which the
    # loader is vendored from. The package's NOTICE.txt covers its build tools (ANTLR), not the loader.
    'SdlVulkan.Renderer.WebView.Native': ('WebView2Loader.dll, the Microsoft Edge WebView2 SDK loader',
                                          'BSD-3-Clause', 'Copyright (C) Microsoft Corporation',
                                          'https://aka.ms/webview2', 'WebView2Loader.txt'),
}

MIT = '''Permission is hereby granted, free of charge, to any person obtaining a copy
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
SOFTWARE.'''


def read_text(path):
    with open(path, encoding='utf-8-sig', errors='replace') as f:
        return f.read().replace('\r\n', '\n').strip('\n')


def nuspec(folder):
    """(copyright, authors, project url, license expression or None, license file or None)."""
    name = next(n for n in os.listdir(folder) if n.endswith('.nuspec'))
    meta = ET.parse(os.path.join(folder, name)).getroot().find('{*}metadata')
    get = lambda tag: (meta.findtext('{*}' + tag) or '').strip()
    repo = meta.find('{*}repository')
    url = get('projectUrl') or (repo.get('url', '') if repo is not None else '')
    lic = meta.find('{*}license')
    kind, value = (lic.get('type'), (lic.text or '').strip()) if lic is not None else (None, '')
    return (get('copyright'), get('authors'), url,
            value if kind == 'expression' else None, value if kind == 'file' else None)


def main(rid, out_path):
    with open(ASSETS, encoding='utf-8') as f:
        assets = json.load(f)
    target = assets['targets'].get(f'{TFM}/{rid}')
    if target is None:
        sys.exit(f'{ASSETS} has no {TFM}/{rid} target: run `dotnet publish -r {rid}` first.')
    roots = list(assets['packageFolders'])

    def folder(key):
        rel = assets['libraries'][key]['path']
        return next(os.path.join(r, rel) for r in roots if os.path.isdir(os.path.join(r, rel)))

    entries = []   # (heading, license, copyright, authors, url)
    texts = {}     # text -> (title or None, [who it applies to])

    def add_text(text, title, who):
        texts.setdefault(text, (title, []))[1].append(who)
        return text

    runtime = None
    for key, lib in sorted(target.items(), key=lambda kv: kv[0].lower().split('/')):
        if lib.get('type') != 'package':
            continue
        package_id, version = key.split('/', 1)
        if package_id.lower() == f'runtime.{rid}.microsoft.dotnet.ilcompiler':
            runtime = (version, folder(key))
            continue
        shipped = [p for p in lib.get('runtime', {}) if not p.endswith('_._')]
        native = [p for p in lib.get('native', {}) if not p.endswith('_._')]
        if not shipped and not native:
            continue
        path = folder(key)
        copyright_, authors, url, expression, file = nuspec(path)
        who = f'{package_id} {version}'
        if file:
            add_text(read_text(os.path.join(path, file)), None, who)
            entries.append((who, '(text below)', copyright_, authors, url))
        elif expression == 'MIT':
            add_text(MIT, 'The MIT License', f'{who} ({copyright_ or authors})')
            entries.append((who, 'MIT', copyright_, authors, url))
        else:
            sys.exit(f'{who}: license {expression or "(none)"} is not handled; add it to this script.')
        if native:
            if package_id not in NATIVE:
                sys.exit(f'{who} ships {native} with no entry in NATIVE: find its license and add one.')
            what, license_, holder, native_url, notice = NATIVE[package_id]
            add_text(read_text(os.path.join(ROOT, 'tools', 'notices', notice)), what, f'{what} (from {who})')
            entries.append((f'{what} (from {who})', license_, holder, '', native_url))

    if runtime is None:
        sys.exit(f'No runtime.{rid}.Microsoft.DotNet.ILCompiler in the graph: is PublishAot still on?')
    version, path = runtime
    dotnet = f'.NET {version} runtime, native AOT'
    add_text(MIT, 'The MIT License', f'{dotnet} (.NET Foundation and Contributors)')
    entries.insert(0, (dotnet, 'MIT, with the notices of its own third-party code below',
                       'Copyright (c) .NET Foundation and Contributors', '', 'https://github.com/dotnet/runtime'))
    add_text(read_text(os.path.join(path, 'THIRD-PARTY-NOTICES.TXT')),
             "The .NET runtime's notices for its own third-party code (THIRD-PARTY-NOTICES.TXT)", dotnet)

    lines = [
        f'YtPlayer ({rid}): third-party notices',
        '=' * 40,
        '',
        f'Generated for this build by tools/third-party-notices.py from the packages it restored.',
        '',
        'YtPlayer itself is MIT-licensed (LICENSE). ytplayer.exe is native AOT: the .NET runtime and the',
        'libraries below are compiled into it, and SDL3.dll and WebView2Loader.dll ship beside it. Their',
        'licenses ask for their notices to go with the binaries; here they are.',
        '',
        '',
        'Inventory',
        '---------',
        '',
    ]
    for heading, license_, holder, authors, url in entries:
        lines.append(f'  {heading}')
        lines.append(f'      license    {license_}')
        if holder:
            lines.append(f'      copyright  {holder}')
        elif authors:
            lines.append(f'      authors    {authors}')
        if url:
            lines.append(f'      project    {url}')
        lines.append('')
    lines += ['', 'License texts', '-------------', '',
              'Verbatim. Where several share a text it is printed once, under all it applies to. For the',
              'MIT License, the copyright holder is the one named against each.', '']
    for text, (title, who) in texts.items():
        heading = [title] if title else []
        lines += ['=' * 78, *heading, 'Applies to:', *(f'  {w}' for w in sorted(set(who))), '=' * 78, '', text, '', '']

    with open(out_path, 'w', encoding='utf-8', newline='\r\n') as f:
        f.write('\n'.join(lines).rstrip('\n') + '\n')
    print(f'{out_path}: {len(entries)} entries, {len(texts)} license texts')


if __name__ == '__main__':
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2])
