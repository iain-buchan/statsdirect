grammar StatsDirectExpression;

options { language='CSharp2'; }
@namespace { StatsDirect.Expressions }

public start returns [string builtExpression]
	: EQ? expr EOF { $start.builtExpression = $expr.builtExpression; }
	;

expr returns [string builtExpression]
	: lhs=andexpr { $expr.builtExpression = $lhs.builtExpression; }
		(op=OR rhs=andexpr { $expr.builtExpression = "(" + $expr.builtExpression + " || " + $rhs.builtExpression + ")"; })* 
	;

andexpr returns [string builtExpression]
	: lhs=notexpr { $andexpr.builtExpression = $lhs.builtExpression; }
		(op=AND rhs=notexpr { $andexpr.builtExpression = "(" + $andexpr.builtExpression + " && " + $rhs.builtExpression + ")"; })* 
	;

notexpr returns [string builtExpression]
	: ( NOT rhs=relexpr { $notexpr.builtExpression = "(!(" + $rhs.builtExpression + "))"; } )
	| relexpr { $notexpr.builtExpression = $relexpr.builtExpression; }
	;

relexpr returns [string builtExpression]
	: lhs=numexpr { $relexpr.builtExpression = $lhs.builtExpression; }
		(op=relop rhs=numexpr { $relexpr.builtExpression = "(" + $relexpr.builtExpression + " " + $op.builtExpression + " " + $rhs.builtExpression + ")"; })? 
	;

numexpr returns [string builtExpression]
	: lhs=mulexpr { $numexpr.builtExpression = $lhs.builtExpression; }
		(op=addop rhs=mulexpr { $numexpr.builtExpression = "(" + $numexpr.builtExpression + " " + $op.builtExpression + " " + $rhs.builtExpression + ")"; })* 
	;
	
mulexpr returns [string builtExpression]
	: lhs=powexpr { $mulexpr.builtExpression = $lhs.builtExpression; }
		(op=mulop rhs=powexpr { if ("idiv".Equals($op.builtExpression)) $mulexpr.builtExpression = "SDMath.Idiv(" + $mulexpr.builtExpression + ", " + $rhs.builtExpression + ")"; else $mulexpr.builtExpression = "(" + $mulexpr.builtExpression + " " + $op.builtExpression + " " + $rhs.builtExpression + ")"; } )*
	;

powexpr returns [string builtExpression]
	: lhs=factorial { $powexpr.builtExpression = $lhs.builtExpression; }
		((CARET | STARSTAR) rhs=factorial { $powexpr.builtExpression = "Math.Pow(" + $powexpr.builtExpression + ", " + $rhs.builtExpression + ")"; } )* 
	;

factorial returns [string builtExpression]
	: term { $factorial.builtExpression = $term.builtExpression; }
	(EXCLAIM { $factorial.builtExpression = "SDMath.Factorial(" + $term.builtExpression + ")"; })?
	;
	
term returns [string builtExpression]
	: INTEGER { $term.builtExpression = "((double)" + $INTEGER.text + ")"; }
	| MINUS INTEGER { $term.builtExpression = "((double)-" + $INTEGER.text + ")"; }
	| FLOAT { $term.builtExpression = double.Parse($FLOAT.text.Replace("d","e").Replace("D","E")).ToString(); }
	| MINUS FLOAT { $term.builtExpression = "-" + double.Parse($FLOAT.text.Replace("d","e").Replace("D","E")).ToString(); }
	| LPAREN expr RPAREN { $term.builtExpression = "(" + $expr.builtExpression + ")"; }
	| constant { $term.builtExpression = $constant.builtExpression; }
	| function { $term.builtExpression = $function.builtExpression; }
	| IDENTIFIER { $term.builtExpression = RenderVariable($IDENTIFIER.text); }
	;
	
function returns [string builtExpression]
	: IDENTIFIER LPAREN argumentlist RPAREN { $function.builtExpression = RenderFunction($IDENTIFIER.text.ToUpper(), $argumentlist.arguments); }
	;
	
argumentlist returns [Arguments arguments]
	: lhs=argument { $argumentlist.arguments = new Arguments(); if (null != $lhs.argument) $argumentlist.arguments.Add($lhs.argument); }
	( COMMA rhs=argument { $argumentlist.arguments.Add($rhs.argument); }) *
	;
	
argument returns [Argument argument]
	: expr { $argument.argument = new Argument($expr.builtExpression); }
	| explicitParameterName GETS expr { $argument.argument = new Argument($explicitParameterName.text, $expr.builtExpression); }
	| { $argument.argument = null; }
	;
	
constant returns [string builtExpression]
	: PI { $constant.builtExpression = "Math.PI"; }
	| EE { $constant.builtExpression = "Math.E"; }
	| FALSE { $constant.builtExpression = "false"; }
	| LR { throw new System.NotImplementedException(); }
	| TRUE { $constant.builtExpression = "true"; }
	;
	
relop returns [string builtExpression]
	: NE { $relop.builtExpression = "!="; }
	| LE { $relop.builtExpression = "<="; }
	| LT { $relop.builtExpression = "<"; }
	| GE { $relop.builtExpression = ">="; }
	| GT { $relop.builtExpression = ">"; }
	| EQ { $relop.builtExpression = "=="; }
	;

addop returns [string builtExpression]
	: PLUS { $addop.builtExpression = "+"; }
	| MINUS { $addop.builtExpression = "-"; }
	;
	
mulop returns [string builtExpression]
	: STAR { $mulop.builtExpression = "*"; }
	| SLASH { $mulop.builtExpression = "/"; }
	| BACKSLASH { $mulop.builtExpression = "idiv"; }
	| MOD { $mulop.builtExpression = "\%"; }
	;

explicitParameterName
	: IDENTIFIER
	;

// Anything below here is lexical analysis

INTEGER :	('0'..'9')+
    ;

FLOAT
    :   ('0'..'9')+ '.' ('0'..'9')* EXPONENT?
    |   '.' ('0'..'9')+ EXPONENT?
    |   ('0'..'9')+ EXPONENT
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


WS:     ( ' '
        | '\t'
        | '\r'
        | '\n'
        ) {$channel=Hidden;}
    ;

STRING
    :  '"' ( ~('\\'|'"') )* '"'
    ;

fragment EXPONENT : ('d'|'D'|'e'|'E') ('+'|'-')? ('0'..'9')+ ;
