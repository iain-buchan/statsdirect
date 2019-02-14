lexer grammar CreoleLexer;

SEA_WS
    :  (' '|'\t'|'\r'? '\n')+
    ;

TAG_OPEN
    : '<' -> pushMode(TAG)
    ;

EXPR_OPEN_COMPOUND
    : '@{' -> pushMode(EXPR_COMPOUND)
    ;

EXPR_OPEN_SIMPLE
    : '@' -> pushMode(EXPR_SIMPLE)
    ;

TEXT
    : ~('<'|'@')+
    ;

//
// tag declarations
//
mode TAG;

TAG_CLOSE
    : '>' -> popMode
    ;

TAG_SLASH_CLOSE
    : '/>' -> popMode
    ;

TAG_SLASH
    : '/'
    ;

//
// lexing mode for attribute values
//
TAG_EQUALS
    : '=' -> pushMode(ATTVALUE)
    ;

TAG_B          : 'b' ;
TAG_BLOCK      : 'block' ;
TAG_BR         : 'br' ;
TAG_CI         : 'ci' ;
TAG_GRANDTOTAL : 'grandtotal' ;
TAG_I          : 'i' ;
TAG_INCLUDE    : 'include' ;
TAG_MODEL      : 'model' ;
TAG_P          : 'p' ;
TAG_PRE        : 'pre' ;
TAG_PVAL       : 'pval' ;
TAG_REPORT     : 'report' ;
TAG_SCORE      : 'score' ;
TAG_SUB        : 'sub' ;
TAG_SUBTITLE   : 'subtitle' ;
TAG_SUBTOTAL   : 'subtotal' ;
TAG_SUP        : 'sup' ;
TAG_TABLE      : 'table' ;
TAG_TD         : 'td' ;
TAG_TH         : 'th' ;
TAG_TITLE      : 'title' ;
TAG_TR         : 'tr' ;
TAG_U          : 'u' ;
TAG_WARN       : 'warn' ;

TAG_NAME
    : TAG_NameStartChar TAG_NameChar*
    ;

TAG_WHITESPACE
    : [ \t\r\n] -> skip
    ;

fragment
HEXDIGIT
    : [a-fA-F0-9]
    ;

fragment
DIGIT
    : [0-9]
    ;

fragment
TAG_NameChar
    : TAG_NameStartChar
    | '-'
    | '_'
    | '.'
    | DIGIT
    | '\u00B7'
    | '\u0300'..'\u036F'
    | '\u203F'..'\u2040'
    ;

fragment
TAG_NameStartChar
    : [:a-zA-Z]
    | '\u2070'..'\u218F'
    | '\u2C00'..'\u2FEF'
    | '\u3001'..'\uD7FF'
    | '\uF900'..'\uFDCF'
    | '\uFDF0'..'\uFFFD'
    ;

//
// attribute values
//
mode ATTVALUE;

// an attribute value may have spaces between the '=' and the value
ATTVALUE_VALUE
    : [ ]* ATTRIBUTE -> popMode
    ;

ATTRIBUTE
    : DOUBLE_QUOTE_STRING
    | SINGLE_QUOTE_STRING
    | ATTCHARS
    | HEXCHARS
    | DECCHARS
    ;

fragment ATTCHAR
    : '-'
    | '_'
    | '.'
    | '/'
    | '+'
    | ','
    | '?'
    | '='
    | ':'
    | ';'
    | '#'
    | [0-9a-zA-Z]
    ;

fragment ATTCHARS
    : ATTCHAR+ ' '?
    ;

fragment HEXCHARS
    : '#' [0-9a-fA-F]+
    ;

fragment DECCHARS
    : [0-9]+ '%'?
    ;

fragment DOUBLE_QUOTE_STRING
    : '"' ~[<"]* '"'
    ;
fragment SINGLE_QUOTE_STRING
    : '\'' ~[<']* '\''
	;

mode EXPR_SIMPLE;

EXPR_SIMPLE_VARIABLE
    : EXPR_VariableStartChar EXPR_VariableChar* -> popMode
    ;

EXPR_SIMPLE_WHITESPACE
    : [ \t\r\n] -> skip
    ;

fragment
EXPR_VariableChar
    : EXPR_VariableStartChar
    | '-'
    | '_'
    | DIGIT
    | '\u00B7'
    | '\u0300'..'\u036F'
    | '\u203F'..'\u2040'
    ;

fragment
EXPR_VariableStartChar
    : [a-zA-Z]
    | '\u2070'..'\u218F'
    | '\u2C00'..'\u2FEF'
    | '\u3001'..'\uD7FF'
    | '\uF900'..'\uFDCF'
    | '\uFDF0'..'\uFFFD'
    ;

mode EXPR_COMPOUND;

EXPR_COMPOUND_FORMAT
	: ':' EXPR_COMPOUND_FORMAT_STRING
	;

EXPR_CLOSE_COMPOUND
	: '}' -> popMode
	;

EXPR_COMPOUND_VARIABLE
    : EXPR_VariableStartChar EXPR_VariableChar*
    ;

fragment EXPR_COMPOUND_FORMAT_STRING
	: 'default'
	| 'chart'
	| 'pval'
	| 'roundx'
	| 'roundu'
	;