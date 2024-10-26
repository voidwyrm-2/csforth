: lfib ( n -- )
    variable n
    0 1
    @ n
    do
        variable b
        variable a
        @ a peek cr
        @ b
        @ b @ a +
    loop
;

20 lfib