grammar StatsDirectExpression;

r returns [string builtExpression]
	: EQ? expr { $builtExpression = $expr.builtExpression; }
	;

expr returns [string builtExpression]
	: lhs=andexpr { $builtExpression = $lhs.builtExpression; }
		(op=OR rhs=andexpr { $builtExpression = "(" + $builtExpression + " || " + $rhs.builtExpression + ")"; })* 
	;

andexpr returns [string builtExpression]
	: lhs=notexpr { $builtExpression = $lhs.builtExpression; }
		(op=AND rhs=notexpr { $builtExpression = "(" + $builtExpression + " && " + $rhs.builtExpression + ")"; })* 
	;

notexpr returns [string builtExpression]
	: ( NOT rhs=relexpr { $builtExpression = "(!(" + $rhs.builtExpression + "))"; } )
	| relexpr { $builtExpression = $relexpr.builtExpression; }
	;

relexpr returns [string builtExpression]
	: lhs=numexpr { $builtExpression = $lhs.builtExpression; }
		(op=relop rhs=numexpr { $builtExpression = "(" + $builtExpression + " " + $op.builtExpression + " " + $rhs.builtExpression + ")"; })? 
	;

numexpr returns [string builtExpression]
	: lhs=mulexpr { $builtExpression = $lhs.builtExpression; }
		(op=addop rhs=mulexpr { $builtExpression = "(" + $builtExpression + " " + $op.builtExpression + " " + $rhs.builtExpression + ")"; })* 
	;
	
mulexpr returns [string builtExpression]
	: lhs=powexpr { $builtExpression = $lhs.builtExpression; }
		(op=mulop rhs=powexpr { if ("idiv".Equals($op.builtExpression)) $builtExpression = "SDMath.Idiv(" + $builtExpression + ", " + $rhs.builtExpression + ")"; else $builtExpression = "(" + $builtExpression + " " + $op.builtExpression + " " + $rhs.builtExpression + ")"; } )*
	;

powexpr returns [string builtExpression]
	: lhs=factorial { $builtExpression = $lhs.builtExpression; }
		((CARET | STARSTAR) rhs=factorial { $builtExpression = "Math.Pow(" + $builtExpression + ", " + $rhs.builtExpression + ")"; } )* 
	;

factorial returns [string builtExpression]
	: term { $builtExpression = $term.builtExpression; }
	(EXCLAIM { $builtExpression = "SDMath.Factorial(" + $term.builtExpression + ")"; })?
	;
	
term returns [string builtExpression]
	: INTEGER { $builtExpression = "((double)" + double.Parse($INTEGER.text).ToString() + ")"; }
	| MINUS INTEGER { $builtExpression = "((double)-" + double.Parse($INTEGER.text).ToString() + ")"; }
	| FLOAT { $builtExpression = double.Parse($FLOAT.text.Replace("d","e").Replace("D","E")).ToString(); }
	| MINUS FLOAT { $builtExpression = "-" + double.Parse($FLOAT.text.Replace("d","e").Replace("D","E")).ToString(); }
	| LPAREN expr RPAREN { $builtExpression = "(" + $expr.builtExpression + ")"; }
	| constant { $builtExpression = $constant.builtExpression; }
	| function { $builtExpression = $function.builtExpression; }
	| IDENTIFIER { $builtExpression = RenderVariable($IDENTIFIER.text); }
	;
	
function returns [string builtExpression]
	: IDENTIFIER LPAREN argumentlist RPAREN { $builtExpression = RenderFunction($IDENTIFIER.text.ToUpper(), $argumentlist.arguments); }
	;
	
argumentlist returns [Arguments arguments]
	: lhs=arg { $arguments = new Arguments(); if (null != $lhs.argument) $arguments.Add($lhs.argument); }
	( COMMA rhs=arg { $arguments.Add($rhs.argument); }) *
	;
	
arg returns [Argument argument]
	: expr { $argument = new Argument($expr.builtExpression); }
	| explicitParameterName GETS expr { $argument = new Argument($explicitParameterName.text, $expr.builtExpression); }
	| { $argument = null; }
	;
	
constant returns [string builtExpression]
	: PI { $builtExpression = "Math.PI"; }
	| EE { $builtExpression = "Math.E"; }
	| FALSE { $builtExpression = "false"; }
	| TRUE { $builtExpression = "true"; }
//	| LR { throw new System.NotImplementedException(); }
	;
	
relop returns [string builtExpression]
	: NE { $builtExpression = "!="; }
	| LE { $builtExpression = "<="; }
	| LT { $builtExpression = "<"; }
	| GE { $builtExpression = ">="; }
	| GT { $builtExpression = ">"; }
	| EQ { $builtExpression = "=="; }
	;

addop returns [string builtExpression]
	: PLUS { $builtExpression = "+"; }
	| MINUS { $builtExpression = "-"; }
	;
	
mulop returns [string builtExpression]
	: STAR { $builtExpression = "*"; }
	| SLASH { $builtExpression = "/"; }
	| BACKSLASH { $builtExpression = "idiv"; }
	| MOD { $builtExpression = "%"; }
	;

explicitParameterName
	: IDENTIFIER
	;
// Anything below here is lexical analysis

INTEGER :	DIGITSANDTHOUSANDS
    ;

FLOAT
    :   DIGITSANDTHOUSANDS DECIMALSEPARATOR ('0'..'9')* EXPONENT?
    |   DECIMALSEPARATOR ('0'..'9')+ EXPONENT?
    |   DIGITSANDTHOUSANDS EXPONENT
    ;

// Tokens.  Implemented in this way to provide cheap, portable case-insensitivity.
AND	:	A N D;
EE	:	E E;
EQ	:	'=';
FALSE: F A L S E;
GE	:	('=' '>') | ('>' '=');
GETS: ':' '=';
GT	:	'>';
LE	:	('<' '=') | ('=' '<');
LR	: L R;
LT	:	'<';
MOD	: M O D;
NE	:	('<' '>') | ('>' '<');
NOT	:	N O T;
OR	:	O R;
PI	:	P I;
TRUE: T R U E;

IDENTIFIER: ('A'..'Z'|'a'..'z')('A'..'Z'|'a'..'z'|'0'..'9'|'.'|'!')*
	;

BACKSLASH	: '\\';
CARET		: '^';
COMMA		: ',';
EXCLAIM		: '!';
LPAREN		: '(';
MINUS		: '-';
PLUS		: '+'; 
RPAREN		: ')';
SLASH		: '/';
STARSTAR	: '*' '*';
STAR		: '*';

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


STRING
    :  '"' ( ~('\\'|'"') )* '"'
    ;

WS:     ( ' '
        | '\t'
        | '\r'
        | '\n'
        ) -> skip
    ;

fragment EXPONENT : ('d'|'D'|'e'|'E') ('+'|'-')? ('0'..'9')+ ;

fragment DIGITSANDTHOUSANDS
	: ('0'..'9')('0'..'9')('0'..'9')('0'..'9')+
	| ('0'..'9')('0'..'9')?('0'..'9')? (THOUSANDSEPARATOR ('0'..'9')('0'..'9')('0'..'9'))*
	;

fragment THOUSANDSEPARATOR
	: {Separators == SeparatorStructure.CommaDot}? ','
	| {Separators == SeparatorStructure.DotComma}? '.'
	| {Separators == SeparatorStructure.SpaceDot}? ' '
	;

fragment DECIMALSEPARATOR
	: {Separators == SeparatorStructure.CommaDot || Separators == SeparatorStructure.SpaceDot}? '.'
	| {Separators == SeparatorStructure.DotComma}? ','
	;
