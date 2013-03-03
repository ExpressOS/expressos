/*
 * The P/Invoke interface implmented by the compiler
 */

#ifndef EXPRESSOS_PINVOKE_H_
#define EXPRESSOS_PINVOKE_H_

struct silk_System_Object
{
        void *methodPtr;
        void *syncRoot; 
};

struct silk_System_String
{
        struct silk_System_Object parent;
        int length;
        short first_char;
};

struct silk_System_Array
{
        struct silk_System_Object obj;
        unsigned int length;
        char base[0];
};

#endif
