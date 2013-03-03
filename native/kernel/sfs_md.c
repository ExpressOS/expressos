/*
 * Verified implementation of the secure file system in C.
 */

#ifdef VERIFY
#include <vcc.h>
#else
#define _(...)
#endif

_(logic \object emb(\object e) = \embedding(e))

#define NULL                  ((void*)0)
#define EINVAL                22
#define DEFAULT_DATA_PGOFFSET 1
#define PAGE_SIZE             4096
#define MINIMUM_HEADER_SIZE   (DEFAULT_DATA_PGOFFSET * PAGE_SIZE)
#define SHA1_SIZE             20
#define SIGNATURE_OFFSET      (20 + 8 + 4 + 4)
#define SFS_MAGIC             0x56414e44524f4944ULL

#define BUFADDR(b, type)      (((char*)(b)+sizeof(type))) // this is what b->buf would evaluate to
#define BUF(b, type, length)  (_(root_index (b)->length) BUFADDR(b, type))  // this is is syntactic sugar expansion
#define BUFA(b, type, length) (emb(BUF(b, type, length)))

struct ByteArray
{
        void     *ptr1;
        void     *ptr2;
        unsigned  length;
        //char data[0];
        _(invariant \mine(BUFA(\this, struct ByteArray, length)))
};

struct sfs_header
{
        unsigned long long   magic;
        char                 hmac[SHA1_SIZE];
        unsigned             data_pgoffset;
        unsigned             file_size;
        unsigned             signature_length;
        _(ghost \bool hmac_verified)
        // char signatures[0];
        _(invariant \mine(BUFA(\this, struct sfs_header, signature_length)))
};

void sfs_calculate_hmac(unsigned data_pgoffset, unsigned file_size,
                        const struct ByteArray *page_signature,
                        char hmac[SHA1_SIZE]);

struct ByteArray *silk_new_byte_array(unsigned size)
        _(requires size > 0)
        _(ensures \result ==> \result->length == size)
        _(ensures \result ==> \fresh(\result))
        _(ensures \result ==> \wrapped(\result))
        ;

_(pure) static int is_sha1_digest_equal(const char lhs[SHA1_SIZE], const char rhs[SHA1_SIZE])
_(reads \array_range(lhs, SHA1_SIZE), \array_range(rhs, SHA1_SIZE))
_(requires \thread_local_array(lhs, SHA1_SIZE))
_(requires \thread_local_array(rhs, SHA1_SIZE))
_(ensures \result == \forall unsigned i; i < SHA1_SIZE ==> lhs[i] == rhs[i])
{
    unsigned k = 0;
    while (k < SHA1_SIZE && lhs[k] == rhs[k])
        _(invariant \forall unsigned i; i < k ==> lhs[i] == rhs[i])
        _(invariant k <= SHA1_SIZE)
    {
        ++k;
    }
    return k == SHA1_SIZE;
}
                
static int sfs_verify_hmac(const char hmac[SHA1_SIZE], unsigned data_pgoffset,
                           unsigned file_size, const struct ByteArray *page_signature)
        _(requires \thread_local_array(hmac, SHA1_SIZE))
{
        char new_hmac[SHA1_SIZE];
        sfs_calculate_hmac(data_pgoffset, file_size, page_signature, new_hmac);
        return is_sha1_digest_equal(hmac, new_hmac);
}

int sfs_initialize_and_verify_metadata(
        struct sfs_header *header, unsigned header_length, unsigned size,
        unsigned *data_pgoffset, unsigned *file_size,
        struct ByteArray **page_signatures)

        _(requires \thread_local(header))
        _(requires header && \wrapped(header))
        _(requires data_pgoffset != file_size)
        _(requires \embedding(file_size) != header)
        _(requires \embedding(data_pgoffset) != header)
        _(writes data_pgoffset)
        _(writes file_size)
        _(writes page_signatures)
        _(writes &header->hmac_verified)
        _(ensures \wrapped(header))
        _(ensures \result == 0 ==> size == 0 ||
          (header->magic == SFS_MAGIC
           && header->data_pgoffset == *data_pgoffset
           && header->file_size == *file_size
           && (\forall unsigned j; j < header_length - SIGNATURE_OFFSET ==>
               BUF(*page_signatures, struct ByteArray, length)[j] ==
               BUF(header, struct sfs_header, signature_length)[j])
           && header->hmac_verified)
          )
{
        unsigned signature_size;
        unsigned i;

        int has_correct_hmac = 0;
        _(unwrapping header) {
                _(ghost header->hmac_verified = 0);
        }

        /* New file */
        if (size == 0) {
                *data_pgoffset = DEFAULT_DATA_PGOFFSET;
                *page_signatures = silk_new_byte_array(PAGE_SIZE * DEFAULT_DATA_PGOFFSET - SIGNATURE_OFFSET);
                *file_size = 0;
                return 0;
        }

        if (header_length < sizeof(struct sfs_header)
            || header_length - SIGNATURE_OFFSET != header->signature_length
            || size < MINIMUM_HEADER_SIZE || header->magic != SFS_MAGIC) {
                *data_pgoffset = 0;
                *page_signatures = NULL;
                *file_size = 0;
                return -EINVAL;
        }

        *data_pgoffset = header->data_pgoffset;
        *file_size = header->file_size;
        signature_size = header_length - SIGNATURE_OFFSET;

        *page_signatures = silk_new_byte_array(signature_size);

        if (!*page_signatures)
                return -1;

        _(unwrapping *page_signatures, BUFA(*page_signatures, struct ByteArray, length)) {
                for (i = 0; i < signature_size; ++i)
                        _(writes \array_range(BUF(*page_signatures, struct ByteArray, length), signature_size))
                        _(invariant \forall unsigned j; j < i ==>
                          BUF(*page_signatures, struct ByteArray, length)[j]
                          == BUF(header, struct sfs_header, signature_length)[j])

                        {
                        BUF(*page_signatures, struct ByteArray, length)[i] =
                                        BUF(header, struct sfs_header, signature_length)[i];
                }
        }

        _(assert \forall unsigned j; j < signature_size ==>
          BUF(*page_signatures, struct ByteArray, length)[j] ==
          BUF(header, struct sfs_header, signature_length)[j]);

        _(unwrapping header) {
                has_correct_hmac = sfs_verify_hmac(header->hmac, *data_pgoffset,
                                                   *file_size, *page_signatures);
                _(ghost header->hmac_verified = has_correct_hmac);
        }
        
        if (!has_correct_hmac) {
                *data_pgoffset = 0;
                *page_signatures = NULL;
                *file_size = 0;
                return -EINVAL;
        }

        _(assert header->magic == SFS_MAGIC && header->data_pgoffset == *data_pgoffset
          && header->file_size == *file_size);

        return 0;
}
