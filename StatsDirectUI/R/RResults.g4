grammar RResults;

@parser::members
{
	protected const int EOF = Eof;
}

@lexer::members
{
	protected const int EOF = Eof;
	protected const int HIDDEN = Hidden;
}

/*
 * Parser Rules
 */

compileUnit returns [Dictionary<string, object> Values]
	: { $compileUnit.Values = new Dictionary<string, object>(); } (stanza { foreach (KeyValuePair<string, object> pair in $stanza.Values) $compileUnit.Values[pair.Key] = pair.Value; })+ EOF
	;

stanza returns [Dictionary<string, object> Values]
	: nameDataPair { $stanza.Values = $nameDataPair.Values; }
	| charts { $stanza.Values = $charts.Values; }
	;

nameDataPair returns [Dictionary<string, object> Values]
	: nameAndValues dataAndValues { $nameDataPair.Values = new Dictionary<string, object>(); for (int i = 0; i < $nameAndValues.Names.Count; i++) $nameDataPair.Values[$nameAndValues.Names[i]] = $dataAndValues.Values[i]; }
	;

charts returns [Dictionary<string, object> Values]
	: STARTGRAPHICS { $charts.Values = new Dictionary<string, object>(); } (path { $charts.Values.Add(PathToName($path.Path), PathToChart($path.Path)); } )*
	;

path returns [string Path]
	: STRING { $path.Path = ToStringBody($STRING.text); }
	;

nameAndValues returns [List<string> Names]
	: STARTNAMES { $nameAndValues.Names = new List<string>(); } (IDENTIFIER { $nameAndValues.Names.Add($IDENTIFIER.text); })*
	;

dataAndValues returns [List<object> Values]
	: STARTDATA { $dataAndValues.Values = new List<object>(); } (expression { $dataAndValues.Values.Add($expression.Value); })*
	;

expression returns [object Value]
	: term { $expression.Value = $term.Value; }
	| vector { $expression.Value = $vector.Terms; }
	;

term returns [object Value]
	: INTEGER { $term.Value = int.Parse($INTEGER.text); }
	| FLOAT { $term.Value = double.Parse($FLOAT.text); }
	| STRING { $term.Value = ToStringBody($STRING.text); }
	;

vector returns [List<object> Terms]
	: STARTVECTOR {$vector.Terms = new List<object>(); } (term { $vector.Terms.Add($term.Value); } COMMA? )* RPAREN
	; 

/*
 * Lexer Rules
 */

INTEGER :	('0'..'9')+
    ;

FLOAT
    :   SIGN? ('0'..'9')+ '.' ('0'..'9')* EXPONENT?
    |   SIGN? '.' ('0'..'9')+ EXPONENT?
    |   SIGN? ('0'..'9')+ EXPONENT
    ;

COMMA		: ',';
DIRSEP		: '\\'|'/';
DRIVE		: ('A'..'Z'|'a'..'z') ':';
RPAREN		: ')';
STARTDATA	:	's' 't' 'a' 'r' 't' '~' 'd' 'a' 't' 'a';
STARTGRAPHICS:	's' 't' 'a' 'r' 't' '~' 'g' 'r' 'a' 'p' 'h' 'i' 'c' 's';
STARTNAMES	:	's' 't' 'a' 'r' 't' '~' 'n' 'a' 'm' 'e' 's';
STARTVECTOR	:	'c' '(';

IDENTIFIER	: ('A'..'Z'|'a'..'z')('A'..'Z'|'a'..'z'|'0'..'9'|'.'|'!')*
	;

WS	
	: (' ' | '\t' | '\r' | '\n') -> skip
    ;

STRING
    :  '"' ( ~('\\'|'"') )* '"'
    ;

fragment EXPONENT : ('d'|'D'|'e'|'E') ('+'|'-')? ('0'..'9')+ ;
fragment SIGN : ('+' | '-');