: i index 1 + ;

: fizzbuzz ( -- )
    100
    do
        i 3 mod 0=
        i 5 mod 0=
        and
        if
            ." fizzbuzz "
        else
            i 3 mod 0=
            if
                ." fizz "
            else
                i 5 mod 0=
                if
                    ." buzz "
                else
                    i . cr
                then
            then
        then
    loop
;

fizzbuzz