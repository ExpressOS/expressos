#include <l4/re/c/log.h>

#define CON_BUF_SIZE 4096

static char con_buf[CON_BUF_SIZE];
static unsigned con_cursor = 0;

void console_flush(void)
{
        l4re_log_printn(con_buf, con_cursor);
        con_buf[con_cursor] = 0;
        con_cursor = 0;
}

int console_putchar(int c)
{
        if (con_cursor >= CON_BUF_SIZE - 1)
                console_flush();

        con_buf[con_cursor++] = (char)c;
        return c;
}

