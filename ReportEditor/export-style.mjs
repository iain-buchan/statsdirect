// Adapt the pinned Mac exporter at build time. The shared exporter now carries
// the report's own presentation (fonts, sizes, colours, borders, padding) into
// Word and Office HTML, so Windows template styles reach them unchanged. What
// stays Windows-specific is the RTF report's layout: pictures and tables sit at
// the left margin at their own width rather than centred across the page, and
// Windows Excel reads image attributes as 96-dpi pixels. Keep upstream files
// unchanged and fail on drift.
export function windowsExportStyle(source) {
  const replace = (from, to) => {
    if (!source.includes(from)) throw Error('Review Windows report export styles: ' + from);
    source = source.replace(from, to);
  };
  replace('alignment:AlignmentType.CENTER,children:[new ImageRun', 'alignment:AlignmentType.LEFT,children:[new ImageRun');
  replace('width:{size:100,type:WidthType.PERCENTAGE},layout:TableLayoutType.AUTOFIT', 'width:{size:0,type:WidthType.AUTO},layout:TableLayoutType.AUTOFIT');
  replace('width:{size:Math.floor(10466*cell.colSpan/columns),type:WidthType.DXA},', 'width:{size:0,type:WidthType.AUTO},');
  // Windows Excel uses 96-dpi pixels for HTML image attributes. The shared Mac
  // clipboard compensates for AppKit's interpretation; that shrinks charts here.
  replace('img.width=Math.round(width*.75);img.height=Math.round(height*.75);', 'img.width=Math.round(width);img.height=Math.round(height);');
  replace('img.width=Math.round(displayWidth*.75);img.height=Math.round(displayHeight*.75);', 'img.width=Math.round(displayWidth);img.height=Math.round(displayHeight);');
  return source;
}
