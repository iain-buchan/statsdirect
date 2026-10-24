"""Validate an exported StatsDirect notebook without executing its R cells.

Requires nbformat, nbconvert and beautifulsoup4. Pass --html to retain the
nbconvert rendering for inspection; expectations come from the report cells.
"""

import argparse
import base64
import json
import re
from pathlib import Path

import nbformat
from bs4 import BeautifulSoup
from nbconvert import HTMLExporter


def require(condition, message):
    if not condition:
        raise ValueError(message)


def text(node):
    return " ".join(node.get_text(" ", strip=True).split())


def validate(path, html_path=None):
    # nbformat.reads rejoins the line arrays Jupyter tooling writes for sources and text outputs; from_dict would hand lists to nbconvert
    notebook = nbformat.reads(path.read_text(encoding="utf-8-sig"), as_version=4)
    ids = [cell.get("id") for cell in notebook.cells]
    require(all(ids) and len(ids) == len(set(ids)), "Cell identifiers must be present and unique")
    nbformat.validate(notebook)
    require(notebook.metadata.get("kernelspec", {}).get("language") == "R", "Expected an R kernel")
    provenance = notebook.metadata.get("statsdirect", {})
    require(isinstance(provenance.get("reportHtml"), str) and provenance["reportHtml"].strip(),
            "Complete original report HTML is missing")
    entries = provenance.get("entries")
    require(isinstance(entries, list), "Original report entries are missing")
    entry_ids = {entry["id"] for entry in entries}
    require(len(entry_ids) == len(entries), "Original report entry identifiers are duplicated")
    preserved = {entry_id: [0, 0] for entry_id in entry_ids}

    exporter = HTMLExporter(template_name="basic")
    rendered, _ = exporter.from_notebook_node(notebook)
    code_count = table_count = image_count = report_count = 0
    for index, cell in enumerate(notebook.cells, 1):
        if cell.cell_type == "code":
            code_count += 1
            require(cell.execution_count is None and cell.outputs == [],
                    f"Code cell {index} has been executed or contains outputs")
        role = cell.metadata.get("statsdirect", {})
        if role.get("role") != "original StatsDirect report":
            continue
        report_count += 1
        require(cell.cell_type == "markdown", f"Original report cell {index} is not Markdown")
        require(role.get("entryId") in entry_ids, f"Report cell {index} has no original entry")
        source = BeautifulSoup(cell.source, "html.parser")
        # Render each original report cell on its own, avoiding dependence on
        # nbconvert's surrounding notebook layout or CSS class names.
        single = nbformat.v4.new_notebook(cells=[cell], metadata=notebook.metadata)
        body, _ = exporter.from_notebook_node(single)
        parsed = BeautifulSoup(body, "html.parser")
        if cell.source.lstrip().startswith("<div>"):
            require(text(source) == text(parsed), f"Report cell {index} changed literal report text")
            expected_lists = [(node.name, node.get("start"), len(node.find_all("li", recursive=False)))
                              for node in source.find_all(["ol", "ul"])]
            actual_lists = [(node.name, node.get("start"), len(node.find_all("li", recursive=False)))
                            for node in parsed.find_all(["ol", "ul"])]
            require(expected_lists == actual_lists, f"Report cell {index} changed list structure")
        expected_tables, actual_tables = source.find_all("table"), parsed.find_all("table")
        require(len(actual_tables) == len(expected_tables), f"Report cell {index} lost a table")
        for expected, actual in zip(expected_tables, actual_tables):
            require(text(expected) == text(actual), f"Report cell {index} changed table text")
            require(len(expected.find_all("tr")) == len(actual.find_all("tr")),
                    f"Report cell {index} changed table rows")
            expected_spans = [(node.name, node.get("rowspan", "1"), node.get("colspan", "1"))
                              for node in expected.find_all(["td", "th"])]
            actual_spans = [(node.name, node.get("rowspan", "1"), node.get("colspan", "1"))
                            for node in actual.find_all(["td", "th"])]
            require(expected_spans == actual_spans, f"Report cell {index} changed table spans")
        table_count += len(expected_tables)

        attachments = cell.get("attachments", {})
        for name, bundle in attachments.items():
            if "image/svg+xml" in bundle:
                require("image/png" in bundle, f"Attachment {name} lacks its PNG fallback")
            if "image/png" in bundle:
                require(base64.b64decode(bundle["image/png"], validate=True).startswith(b"\x89PNG\r\n\x1a\n"),
                        f"Attachment {name} is not a PNG")
        references = re.findall(r"!\[[^\]]*\]\(attachment:([^\s)]+)\)", cell.source)
        require(all(name in attachments for name in references),
                f"Report cell {index} refers to a missing image attachment")
        expected_images = len(source.find_all("img")) + len(references)
        images = parsed.find_all("img")
        require(len(images) == expected_images, f"Report cell {index} lost an image")
        for picture in images:
            require(picture.get("src", "").startswith("data:image/"),
                    f"Report cell {index} has an unresolved or external image")
        image_count += len(images)
        preserved[role["entryId"]][0] += len(expected_tables)
        preserved[role["entryId"]][1] += len(images)

    for entry in entries:
        original = BeautifulSoup(entry["html"], "html.parser")
        pictures = [node for node in original.find_all(["img", "svg", "canvas"])
                    if not node.find_parent(["svg", "canvas"])]
        require(preserved[entry["id"]] == [len(original.find_all("table")), len(pictures)],
                f"Original report entry {entry['id']} lost tables or pictures from its report cells")

    if html_path:
        html_path.write_text(rendered, encoding="utf-8")
    print(f"PASS {path}: nbformat valid; {code_count} unexecuted R cells; "
          f"{report_count} original report cells, {table_count} tables and {image_count} images render")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("notebook", type=Path)
    parser.add_argument("--html", type=Path, help="Save the rendered HTML for inspection")
    arguments = parser.parse_args()
    try:
        validate(arguments.notebook, arguments.html)
    except (ValueError, KeyError, OSError) as error:
        parser.exit(1, f"FAIL {error}\n")
