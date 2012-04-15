grammar ActiveRtf;

options { language='CSharp2'; }
@namespace { StatsDirect.TextProcessing }

public start returns [string code]
	: activeRtf EOF { $start.code = $activeRtf.code; }
	;

activeRtf returns [string code]
	: first=piece { $activeRtf.code = $first.code; }
		(rest=piece { $activeRtf.code += $rest.code; }) *
	;

piece returns [string code]
	: substitution { $piece.code = $substitution.code; }
	| block { $piece.code = $block.code; }
	| bumf { $piece.code = $bumf.code; }
	;

substitution returns [string code]
	: SLASHSTAR WS? VARIABLENAME=IDENTIFIER ( WS? COLON WS? VARIABLEFORMAT=IDENTIFIER )? WS? SLASH { $substitution.code = EncodeSubstitution($VARIABLENAME.text, $VARIABLEFORMAT.text); }
	;

block returns [string code]
	: blockStart activeRtf blockFinish { $block.code = EncodeBlock($blockStart.name, $activeRtf.code); }
	;

bumf returns [string code]
	: bumfPieces { $bumf.code = EncodeBumf($bumfPieces.text); }
	;

bumfPieces returns [string text]
	: first=bumfPiece { $bumfPieces.text = $first.text; }
		(rest=bumfPiece { $bumfPieces.text += $rest.text; }) *
	;

bumfPiece returns [string text]
	: BUMF
	| COLON
	| IDENTIFIER
	| SLASH
	| WS
	;

blockStart returns [string name]
	: SLASHBS WS? IDENTIFIER WS? SLASH { $blockStart.name = $IDENTIFIER.text; }
	;

blockFinish
	: SLASHBF SLASH
	;

// Anything below here is lexical analysis

// Tokens.  Implemented in this way to provide cheap, portable case-insensitivity.

// Highest priority: Starts of processing
SLASHBF	:	'/' B F;
SLASHBS	:	'/' B S;
SLASHSTAR	:	'/' '*';

// Mid-priority: Anything we need to parse inside a processing directive
COLON 	:	':';
IDENTIFIER: ('A'..'Z'|'a'..'z')('A'..'Z'|'a'..'z'|'0'..'9')*;
SLASH		: '/';
WS:     ( ' ' | '\t' | '\r' | '\n' );

// Lowest priority: a catch-all for everything else
BUMF	:	(~ ('/' | ':' | ' ' | '\t' | '\r' | '\n' | 'A'..'Z'  |'a'..'z'))+;

fragment A	:	'A'|'a';
fragment B	:	'B'|'b';
fragment C	:	'C'|'c';
fragment D	:	'D'|'d';
fragment E	:	'E'|'e';
fragment F	:	'F'|'f';
fragment G	:	'G'|'g';
fragment H	:	'H'|'h';
fragment I	:	'I'|'i';
fragment J	:	'J'|'j';
fragment K	:	'K'|'k';
fragment L	:	'L'|'l';
fragment M	:	'M'|'m';
fragment N	:	'N'|'n';
fragment O	:	'O'|'o';
fragment P	:	'P'|'p';
fragment Q	:	'Q'|'q';
fragment R	:	'R'|'r';
fragment S	:	'S'|'s';
fragment T	:	'T'|'t';
fragment U	:	'U'|'u';
fragment V	:	'V'|'v';
fragment W	:	'W'|'w';
fragment X	:	'X'|'x';
fragment Y	:	'Y'|'y';
fragment Z	:	'Z'|'z';
