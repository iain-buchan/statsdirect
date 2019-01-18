parser grammar CreoleParser;

options { tokenVocab=CreoleLexer; }

document
    : (substitution | SEA_WS)* elements* EOF
    ;

elements
    : misc* element misc*
    ;

element
	: TAG_OPEN openTag=TAG_NAME attribute* TAG_CLOSE content TAG_OPEN TAG_SLASH closeTag=TAG_NAME TAG_CLOSE {TagsMatch($openTag, $closeTag)}?
    | TAG_OPEN TAG_NAME attribute* TAG_SLASH_CLOSE
    | TAG_OPEN TAG_NAME attribute* TAG_CLOSE
    | substitution
    ;

content
    : chardata? ((element | cdata | comment) chardata?)*
    ;

attribute
    : attributeName TAG_EQUALS attributeValue
    | attributeName
    ;

attributeName
    : TAG_NAME
    ;

attributeValue
    : ATTVALUE_VALUE
    ;

chardata
    : TEXT
    | SEA_WS
    ;

misc
    : comment
    | SEA_WS
    ;

comment
    : COMMENT
    ;

cdata
    : CDATA
    ;

substitution
    : SUBSTITUTION
    ;
