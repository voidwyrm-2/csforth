( doesn't work )
: rfib ( n -- )
    dup 2 <
    dup cr .
    if 
        1 = if 1 else 0 then
    else
        dup
        1 - rfib
        2 - rfib
        +
    then
;

20 rfib