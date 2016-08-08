grammar Rtf ;

document returns [Chunks retval]
	: chunks { $retval = $chunks.retval; } EOF
	;

chunks returns [Chunks retval]
	: { $retval = new Chunks(); } (chunk { $retval.Add($chunk.retval); })+
	;

chunk returns [Chunk retval]
	: control { $retval = $control.retval; }
	| entity { $retval = $entity.retval; }
	| group { $retval = $group.retval; }
	| substitution { $retval = $substitution.retval; }
	| blockstart { $retval = $blockstart.retval; }
	| blockfinish { $retval = $blockfinish.retval; }
	| { $retval = new StringChunk(); } (frag=text { ((StringChunk)$retval).Append($frag.text); })+
	;

control returns [RtfControl retval]
	: KEYWORD INT? SPACE? { $retval = new RtfControl($KEYWORD.text.Substring(1), $INT?.Text); }
	;

group returns [Chunk retval]
	: OPENBRACE chunks CLOSEBRACE { $retval = $chunks.retval; }
	;

substitution returns [Substitution retval]
	: SLASHSTAR IDENTIFIER SLASH { $retval = new Substitution($IDENTIFIER.text); }
	;

blockstart returns [BlockStart retval]
	: SLASHBS SPACE IDENTIFIER SLASH  { $retval = new BlockStart($IDENTIFIER.text); }
	;

blockfinish returns [BlockFinish retval]
	: SLASHBFSLASH { $retval = new BlockFinish(); }
	;

text
	: TEXT
	| SPACE
    | INT
	| IDENTIFIER
	| SLASH
	;

entity returns [Entity retval]
	: ENTITY { $retval = new Entity($ENTITY.text.Substring(2)); }
	;

KEYWORD
	: '\\' (ASCIILETTER)+
	| '\\' '*'
	;

ENTITY
	: '\\' '\'' HEXDIGIT+
	;

fragment ASCIILETTER : [A-Za-z] ;
fragment DIGIT : [0-9] ;
fragment HEXDIGIT : [0-9A-Fa-f] ;

INT : '-'? DIGIT+ ;
IDENTIFIER : (ASCIILETTER | '+' | '-') (ASCIILETTER | DIGIT | '_' | '-' | '*')* ;

SLASHBS : '/''b''s';
SLASHBFSLASH : '/''b''f''/';
SLASHSTAR : '/''*';
SLASH : '/';
SPACE : [ \r\n] ;
OPENBRACE : '{' ;
CLOSEBRACE : '}' ;
TEXT: . ;