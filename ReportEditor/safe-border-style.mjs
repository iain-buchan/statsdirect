// CSSStyleDeclaration enumerates border shorthand as its twelve longhand
// properties. Keep this finite presentation allowlist in both sanitizers;
// otherwise safeStyle silently drops table borders on copy or HTML reopening.
export const safeBorderStyle = {
  name: 'windows-safe-border-styles',
  setup(build) {
    build.onLoad({filter:/(fragment|import)\.mjs$/}, async ({path}) => {
      const {readFile} = await import('node:fs/promises');
      let contents = await readFile(path, 'utf8');
      const marker = 'function safeStyle(style) {';
      if(!contents.includes(marker)) throw Error('Review report style sanitizer: '+path);
      contents = contents.replace(marker, `for(const edge of ['top','right','bottom','left'])
  for(const part of ['width','style','color'])properties.add('border-'+edge+'-'+part);
${marker}`);
      return {contents, loader:'js'};
    });
  }
};
