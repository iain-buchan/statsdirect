grammar StatsDirectExpression;

r returns [INode node]
	: EQ? expr { $node = $expr.node; }
	;

expr returns [INode node]
	: lhs=expr op=OR rhs=andexpr { $node = new DyadicNode { Left = $lhs.node, Operator = DyadicOperator.Or, Right = $rhs.node }; }
	| andexpr { $node = $andexpr.node; }
	;

andexpr returns [INode node]
	: lhs=andexpr op=AND rhs=notexpr { $node = new DyadicNode { Left = $lhs.node, Operator = DyadicOperator.And, Right = $rhs.node }; }
	| notexpr { $node = $notexpr.node; }
	;

notexpr returns [INode node]
	: NOT rhs=relexpr { $node = new MonadicNode { Operator = MonadicOperator.Not, Node = $rhs.node }; }
	| relexpr { $node = $relexpr.node; }
	;

relexpr returns [INode node]
	: lhs=numexpr op=relop rhs=numexpr { $node = new DyadicNode { Left = $lhs.node, Operator = $op.operator, Right = $rhs.node }; }
	| numexpr { $node = $numexpr.node; }
	;

numexpr returns [INode node]
	: lhs=numexpr op=addop rhs=mulexpr { $node = new DyadicNode { Left = $lhs.node, Operator = $op.operator, Right = $rhs.node }; }
	| mulexpr { $node = $mulexpr.node; }
	;
	
mulexpr returns [INode node]
	: lhs=mulexpr op=mulop rhs=powexpr { $node = new DyadicNode { Left = $lhs.node, Operator = $op.operator, Right = $rhs.node }; }
	| powexpr { $node = $powexpr.node; }
	;

powexpr returns [INode node]
	: lhs=powexpr (CARET | STARSTAR) rhs=factorial { $node = new DyadicNode { Left = $lhs.node, Operator = DyadicOperator.Pow, Right = $rhs.node }; }
	| factorial { $node = $factorial.node; }
	;

factorial returns [INode node]
	: term { $node = $term.node; }
	| term EXCLAIM { $node = new MonadicNode { Operator = MonadicOperator.Factorial, Node = $term.node }; }
	;
	
term returns [INode node]
	: INTEGER { $node = new IntegerNode { Value = int.Parse($INTEGER.text) }; }
	| MINUS INTEGER { $node = new IntegerNode { Value = 0 - int.Parse($INTEGER.text) }; }
	| FLOAT { $node = new DoubleNode { Value = double.Parse($FLOAT.text.Replace("d","e").Replace("D","E")) }; }
	| MINUS FLOAT { $node = new DoubleNode { Value = 0.0 - double.Parse($FLOAT.text.Replace("d","e").Replace("D","E")) }; }
	| STRING { $node = ParseString($STRING.text); }
	| LPAREN expr RPAREN { $node = $expr.node; }
	| constant { $node = $constant.node; }
	| function { $node = $function.node; }
	| IDENTIFIER { $node = ParseVariable($IDENTIFIER.text); }
	;
	
function returns [FunctionNode node]
	: functionName=IDENTIFIER LPAREN argumentlist RPAREN { $node = new FunctionNode { Name = $functionName.text.ToUpper(), Arguments = $argumentlist.arguments }; }
	;
	
argumentlist returns [Arguments arguments]
	: lhs=arg { $arguments = new Arguments(); if (null != $lhs.argument) $arguments.Add($lhs.argument); }
	( COMMA rhs=arg { $arguments.Add($rhs.argument); }) *
	;
	
arg returns [Argument argument]
	: expr { $argument = new Argument { Node = $expr.node }; }
	| explicitParameterName GETS expr { $argument = new Argument { ExplicitParameterName = $explicitParameterName.text, Node = $expr.node }; }
	| { $argument = null; }
	;
	
constant returns [ConstantNode node]
	: PI { $node = new ConstantNode { Constant = ParserConstant.Pi }; }
	| EE { $node = new ConstantNode { Constant = ParserConstant.E }; }
	| FALSE { $node = new ConstantNode { Constant = ParserConstant.False }; }
	| TRUE { $node = new ConstantNode { Constant = ParserConstant.True }; }
	;
	
relop returns [DyadicOperator operator]
	: NE { $operator = DyadicOperator.NotEqual; }
	| LE { $operator = DyadicOperator.LessThanOrEqual; }
	| LT { $operator = DyadicOperator.LessThan; }
	| GE { $operator = DyadicOperator.GreaterThanOrEqual; }
	| GT { $operator = DyadicOperator.GreaterThan; }
	| EQ { $operator = DyadicOperator.Equal; }
	;

addop returns [DyadicOperator operator]
	: PLUS { $operator = DyadicOperator.Add; }
	| MINUS { $operator = DyadicOperator.Subtract; }
	;
	
mulop returns [DyadicOperator operator]
	: STAR { $operator = DyadicOperator.Multiply; }
	| SLASH { $operator = DyadicOperator.Divide; }
	| BACKSLASH { $operator = DyadicOperator.IntegerDivide; }
	| MOD { $operator = DyadicOperator.Modulo; }
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
