// Adapt the pinned Mac exporter at build time. Windows uses the existing
// Creole/RTF report styles, including in Office; do not restyle its output as
// a generic web document. Keep upstream files unchanged and fail on drift.
export function windowsExportStyle(source) {
  const replace = (from, to) => {
    if (!source.includes(from)) throw Error('Review Windows report export styles: ' + from);
    source = source.replace(from, to);
  };
  const printStart = source.indexOf('const printCSS = `');
  const printEnd = source.indexOf('`;', printStart);
  if (printStart < 0 || printEnd < 0) throw Error('Missing shared print stylesheet');
  // snapshot already includes windows.css and resolved inline styles.
  source = source.slice(0, printStart) + 'const printCSS = `@page{size:A4;margin:16mm}tr,svg,img{break-inside:avoid}thead{display:table-header-group}`;' + source.slice(printEnd + 2);
  replace('const result={alignment,spacing:{after:110,...options.spacing},indent:options.indent};',
    'const result={alignment,spacing:{before:Math.round((parseFloat(css.marginTop)||0)*15),after:Math.round((parseFloat(css.marginBottom)||0)*15),...options.spacing},indent:options.indent};');
  replace('spacing:{after:110,...options.spacing}', 'spacing:{after:0,...options.spacing}');
  replace('spacing:{...paragraphStyle(child,options).spacing,before:180,after:100}', 'spacing:paragraphStyle(child,options).spacing');
  replace("text:line,font:'Courier New',size:16", "...runStyle(child,options),text:line,font:'Courier New'");
  replace('alignment:AlignmentType.CENTER,children:[new ImageRun', 'alignment:AlignmentType.LEFT,children:[new ImageRun');
  replace("const border={style:BorderStyle.SINGLE,size:4,color:'D5DFE3'};", `const border=(cell,edge)=>{
    const css=cell.style,kind=css.getPropertyValue('border-'+edge+'-style'),width=parseFloat(css.getPropertyValue('border-'+edge+'-width'))||0;
    return !width||kind==='none'||kind==='hidden'?{style:BorderStyle.NONE,size:0,color:'000000'}:
      {style:({dashed:BorderStyle.DASHED,dotted:BorderStyle.DOTTED,double:BorderStyle.DOUBLE}[kind]||BorderStyle.SINGLE),size:Math.round(width*6),color:wordColor(css.getPropertyValue('border-'+edge+'-color'))||'000000'};
  };`);
  replace('width:{size:100,type:WidthType.PERCENTAGE},layout:TableLayoutType.AUTOFIT',
    "width:{size:0,type:WidthType.AUTO},layout:TableLayoutType.AUTOFIT,borders:Object.fromEntries(['top','bottom','left','right','insideHorizontal','insideVertical'].map(edge=>[edge,{style:BorderStyle.NONE,size:0,color:'000000'}]))");
  replace("{bold:cell.tagName==='TH',size:19,alignment:numericValue(text(cell))!==null?AlignmentType.RIGHT:AlignmentType.LEFT}",
    '{...runStyle(cell),...paragraphStyle(cell)}');
  replace('width:{size:Math.floor(10466*cell.colSpan/columns),type:WidthType.DXA},margins:{top:65,bottom:65,left:90,right:90},',
    "width:{size:0,type:WidthType.AUTO},margins:Object.fromEntries(['top','bottom','left','right'].map(edge=>[edge,Math.round((parseFloat(cell.style.getPropertyValue('padding-'+edge))||0)*15)])),");
  replace("borders:{top:border,bottom:border,left:border,right:border},shading:cell.tagName==='TH'?{fill:'EDF4F5'}:undefined,",
    "borders:Object.fromEntries(['top','bottom','left','right'].map(edge=>[edge,border(cell,edge)])),shading:wordColor(cell.style.backgroundColor)?{fill:wordColor(cell.style.backgroundColor)}:undefined,");
  replace("run:{font:'Arial',size:22,color:'182B38'},paragraph:{spacing:{after:110}}", "run:{font:'Arial',size:20,color:'000000'},paragraph:{spacing:{after:0}}");
  replace("run:{bold:true,size:34,color:'182B38'}", "run:{bold:true,underline:{},size:20,color:'000000'}");
  replace("run:{bold:true,size:27,color:'182B38'}", "run:{bold:false,underline:{},size:20,color:'000000'}");
  // The shared Excel adapter computes widths and numeric/text hints, but also
  // replaces all table/cell styles. Retain those hints while restoring the
  // selection's actual presentation (including deliberate user formatting).
  replace('  formatClipboardTables(root);', `  const tableStyles=[...root.querySelectorAll('table,td,th')].map(element=>({element,style:element.getAttribute('style')}));
  formatClipboardTables(root);
  for(const {element,style} of tableStyles){
    // Chromium ignores Office-only properties in CSSStyleDeclaration, so
    // retain this generated hint from the literal attribute instead.
    const numberFormat=element.getAttribute('style')?.match(/(?:^|;)\\s*(mso-number-format\\s*:[^;]*;?)/i)?.[1];
    element.removeAttribute('border');element.style.cssText=style??'';
    if(numberFormat)element.setAttribute('style',element.style.cssText+';'+numberFormat);
  }
  // Office interprets CSS pixels using the Windows display DPI. Physical point
  // units keep a 10pt report at 10pt even on a 250% monitor. Preserve Excel's
  // literal number-format hint when CSSStyleDeclaration rewrites the attribute.
  const pointProperties=['font-size','line-height','width','height','max-width',
    ...['top','right','bottom','left'].flatMap(e=>['margin-'+e,'padding-'+e,'border-'+e+'-width'])];
  for(const element of root.querySelectorAll('[style]')) {
    const numberFormat=element.getAttribute('style')?.match(/(?:^|;)\\s*(mso-number-format\\s*:[^;]*;?)/i)?.[1];
    for(const property of pointProperties) {
      const value=element.style.getPropertyValue(property);
      if(/^-?[\\d.]+px$/.test(value))element.style.setProperty(property,Number((parseFloat(value)*.75).toFixed(4))+'pt');
    }
    if(numberFormat&&!element.getAttribute('style').includes('mso-number-format'))element.setAttribute('style',element.style.cssText+';'+numberFormat);
  }`);
  replace('body,p,td,th{font:11pt Arial}h1,h2,h3{font: bold 12pt Arial}td,th{white-space:normal}p{margin:6pt 0}',
    'body,p,td,th{font:10pt Arial;color:#000;line-height:1.2}h1,h2,h3{font:bold 10pt Arial;text-decoration:underline}h2,th{font-weight:normal;text-decoration:underline}td,th{white-space:normal;text-align:left}p{margin:0}');
  // Windows Excel uses 96-dpi pixels for HTML image attributes. The shared Mac
  // clipboard compensates for AppKit's interpretation; that shrinks charts here.
  replace('img.width=Math.round(width*.75);img.height=Math.round(height*.75);',
    'img.width=Math.round(width);img.height=Math.round(height);');
  replace('img.width=Math.round(displayWidth*.75);img.height=Math.round(displayHeight*.75);',
    'img.width=Math.round(displayWidth);img.height=Math.round(displayHeight);');
  return source;
}
