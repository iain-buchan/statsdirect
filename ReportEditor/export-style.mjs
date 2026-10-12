// Adapt the shared Mac exporter at build time. The shared exporter carries
// the report's own presentation (fonts, sizes, colours, borders, padding) into
// Word and Office HTML, so Windows template styles reach them unchanged. What
// stays Windows-specific is automatic table width and 96-dpi image attributes
// for Excel. The chosen chart alignment is shared and must survive export.
// Keep these platform substitutions out of the shared files and fail on drift.
export function windowsExportStyle(source) {
  const replace = (from, to) => {
    if (!source.includes(from)) throw Error('Review Windows report export styles: ' + from);
    source = source.replace(from, to);
  };
  if (!source.includes('alignment:p.alignment,children:[new ImageRun'))
    throw Error('Review Windows report export styles: preserve the chosen chart alignment');
  replace('width:{size:100,type:WidthType.PERCENTAGE},layout:TableLayoutType.AUTOFIT', 'width:{size:0,type:WidthType.AUTO},layout:TableLayoutType.AUTOFIT');
  replace('width:{size:Math.floor(10466*cell.colSpan/columns),type:WidthType.DXA},', 'width:{size:0,type:WidthType.AUTO},');
  // Windows Excel uses 96-dpi pixels for HTML image attributes. The shared Mac
  // clipboard compensates for AppKit's interpretation; that shrinks charts here.
  replace('img.width=Math.round(width*.75);img.height=Math.round(height*.75);', 'img.width=Math.round(width);img.height=Math.round(height);');
  replace('img.width=Math.round(displayWidth*.75);img.height=Math.round(displayHeight*.75);', 'img.width=Math.round(displayWidth);img.height=Math.round(displayHeight);');
  return source;
}
