#include "expressos/mm.h"
#include "expressos/string.h"
#include "expressos/pinvoke.h"

void *__silk_rt_new_object(unsigned object_size)
{
        void *r = gc_malloc(object_size);
        return memset(r, 0, object_size);
}

void *__silk_rt_new_array(unsigned length, unsigned element_size)
{
        size_t s = sizeof(struct silk_System_Array) + element_size * length;
        struct silk_System_Array *r = (struct silk_System_Array*)gc_malloc(s);
        memset(r, 0, s);
        r->length = length;
        return r;
}

void *__silk_rt_array_base_ptr(void *array)
{
        return &(((struct silk_System_Array*)array)->base);
}

struct silk_System_String *InternalAllocateString(int len)
{
        size_t s = sizeof(struct silk_System_String) + (len - 1) * sizeof(short);
        void *r = __silk_rt_new_object(s);
        struct silk_System_String *v = (struct silk_System_String*)r;
        v->length = len;
        return v;
}

void *silk_new_byte_array(unsigned size)
{
        return __silk_rt_new_array(size, 1);
}
