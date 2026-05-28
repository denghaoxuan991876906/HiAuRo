#!/usr/bin/env python3
"""将 .nuspec 打包为 .nupkg（含 HiAuRo.dll + 依赖引用程序集），输出到指定目录。
用法: nuspec_pack.py <nuspec路径> <HiAuRo输出目录> <DALAMUD_HOME> <输出目录>"""

import sys
import os
import uuid
import zipfile
import xml.etree.ElementTree as ET
from datetime import datetime, timezone


DALAMUD_DLLS = [
    "Dalamud.dll", "Dalamud.Common.dll", "Dalamud.Bindings.ImGui.dll",
    "FFXIVClientStructs.dll", "Lumina.dll", "Lumina.Excel.dll",
    "ImGuiScene.dll", "TerraFX.Interop.Windows.dll",
    "Newtonsoft.Json.dll", "Serilog.dll", "CheapLoc.dll",
]


def create_nupkg(nuspec_path: str, build_out_dir: str, dalamud_home: str, output_dir: str) -> str:
    tree = ET.parse(nuspec_path)
    root = tree.getroot()

    ns = {"ns": root.tag.split("}")[0].strip("{")} if "}" in root.tag else {}
    meta = root.find("ns:metadata" if ns else "metadata", ns) if ns else root.find("metadata")
    if meta is None:
        meta = root

    pid = meta.findtext("ns:id" if ns else "id", namespaces=ns) or meta.findtext("id")
    ver = meta.findtext("ns:version" if ns else "version", namespaces=ns) or meta.findtext("version", "0.1.0")

    package_name = f"{pid}.{ver}.nupkg"
    output_path = os.path.join(output_dir, package_name)
    os.makedirs(output_dir, exist_ok=True)

    content_types_xml = '<?xml version="1.0" encoding="utf-8"?>\n'
    content_types_xml += '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">\n'
    content_types_xml += '  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />\n'
    content_types_xml += '  <Default Extension="psmdcp" ContentType="application/vnd.openxmlformats-package.core-properties+xml" />\n'
    content_types_xml += '  <Default Extension="dll" ContentType="application/octet" />\n'
    content_types_xml += '  <Default Extension="json" ContentType="application/octet" />\n'
    nuspec_filename = os.path.basename(nuspec_path)
    content_types_xml += f'  <Override PartName="/{nuspec_filename}" ContentType="application/octet" />\n'
    content_types_xml += '</Types>'

    rels_xml = '<?xml version="1.0" encoding="utf-8"?>\n'
    rels_xml += '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">\n'
    rels_xml += '</Relationships>'

    core_props_id = str(uuid.uuid4())
    now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    psmdcp_xml = '<?xml version="1.0" encoding="utf-8"?>\n'
    psmdcp_xml += '<coreProperties xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns="http://schemas.openxmlformats.org/package/2006/metadata/core-properties">\n'
    psmdcp_xml += f'  <dc:creator>HiAuRo</dc:creator>\n'
    psmdcp_xml += f'  <dc:description>{pid}</dc:description>\n'
    psmdcp_xml += f'  <dc:identifier>{pid}</dc:identifier>\n'
    psmdcp_xml += f'  <version>{ver}</version>\n'
    psmdcp_xml += f'  <keywords></keywords>\n'
    psmdcp_xml += f'  <lastModifiedBy>HiAuRo</lastModifiedBy>\n'
    psmdcp_xml += f'  <created>{now}</created>\n'
    psmdcp_xml += f'  <modified>{now}</modified>\n'
    psmdcp_xml += f'  <contentType>NuGet Package</contentType>\n'
    psmdcp_xml += '</coreProperties>'

    missing = []
    with zipfile.ZipFile(output_path, "w", zipfile.ZIP_DEFLATED) as zf:
        zf.writestr("[Content_Types].xml", content_types_xml)
        zf.writestr("_rels/.rels", rels_xml)
        zf.writestr(f"package/services/metadata/core-properties/{core_props_id}.psmdcp", psmdcp_xml)
        zf.write(nuspec_path, nuspec_filename)

        # 核心 DLL
        for dll in ["HiAuRo.dll", "OmenTools.dll"]:
            src = os.path.join(build_out_dir, dll)
            if os.path.isfile(src):
                zf.write(src, f"lib/net10.0/{dll}")
            else:
                missing.append(dll)

        # HiAuRo.deps.json
        deps = os.path.join(build_out_dir, "HiAuRo.deps.json")
        if os.path.isfile(deps):
            zf.write(deps, "lib/net10.0/HiAuRo.deps.json")

        # Dalamud 引用程序集
        if dalamud_home and os.path.isdir(dalamud_home):
            for dll in DALAMUD_DLLS:
                src = os.path.join(dalamud_home, dll)
                if os.path.isfile(src):
                    zf.write(src, f"lib/net10.0/{dll}")
                else:
                    missing.append(dll)

    if missing:
        print(f"⚠ 未找到以下 DLL（已跳过）: {', '.join(missing)}")

    return output_path


if __name__ == "__main__":
    if len(sys.argv) < 4:
        print(f"用法: {sys.argv[0]} <nuspec路径> <HiAuRo输出目录> [DALAMUD_HOME] <输出目录>", file=sys.stderr)
        sys.exit(1)

    nuspec = sys.argv[1]
    build_dir = sys.argv[2]
    if len(sys.argv) == 4:
        dalamud = ""
        out = sys.argv[3]
    else:
        dalamud = sys.argv[3]
        out = sys.argv[4]

    try:
        result = create_nupkg(nuspec, build_dir, dalamud, out)
        print(f"[HiAuRo] SDK packed: {result}")
    except Exception as e:
        print(f"[HiAuRo] SDK pack failed: {e}", file=sys.stderr)
        sys.exit(1)
