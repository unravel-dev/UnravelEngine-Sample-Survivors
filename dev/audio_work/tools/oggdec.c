/* Decodes Ogg Vorbis files with the engine's own stb_vorbis copy into raw float files:
 *   "RAW1" | int32 channels | int32 rate | int32 frames | float32 interleaved samples
 * Usage: oggdec <list.txt>   (each line: input.ogg|output.raw)
 */
#define STB_VORBIS_IMPLEMENTATION
#include "C:/Workspace/github/UnravelEngine/UnravelEngine/deps/audiopp/audiopp/audiopp/loaders/decoders/decoder_vorbis.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static int decode_one(const char* in_path, const char* out_path)
{
    int error = 0;
    stb_vorbis* v = stb_vorbis_open_filename(in_path, &error, NULL);
    if(!v)
    {
        fprintf(stderr, "open failed %d: %s\n", error, in_path);
        return 1;
    }
    stb_vorbis_info info = stb_vorbis_get_info(v);
    int channels = info.channels;
    int rate = (int)info.sample_rate;
    size_t capacity = 1 << 16;
    size_t count = 0;
    float* data = (float*)malloc(capacity * channels * sizeof(float));
    float chunk[4096 * 8];
    for(;;)
    {
        int got = stb_vorbis_get_samples_float_interleaved(v, channels, chunk, 4096 * channels);
        if(got <= 0)
        {
            break;
        }
        if(count + got > capacity)
        {
            while(count + got > capacity)
            {
                capacity *= 2;
            }
            data = (float*)realloc(data, capacity * channels * sizeof(float));
        }
        memcpy(data + count * channels, chunk, (size_t)got * channels * sizeof(float));
        count += got;
    }
    stb_vorbis_close(v);
    FILE* out = fopen(out_path, "wb");
    if(!out)
    {
        fprintf(stderr, "write failed: %s\n", out_path);
        free(data);
        return 1;
    }
    int frames = (int)count;
    fwrite("RAW1", 1, 4, out);
    fwrite(&channels, 4, 1, out);
    fwrite(&rate, 4, 1, out);
    fwrite(&frames, 4, 1, out);
    fwrite(data, sizeof(float), count * channels, out);
    fclose(out);
    free(data);
    return 0;
}

int main(int argc, char** argv)
{
    if(argc < 2)
    {
        fprintf(stderr, "usage: oggdec list.txt\n");
        return 2;
    }
    FILE* list = fopen(argv[1], "rb");
    if(!list)
    {
        return 2;
    }
    char line[4096];
    int failures = 0;
    while(fgets(line, sizeof(line), list))
    {
        size_t n = strlen(line);
        while(n > 0 && (line[n - 1] == '\n' || line[n - 1] == '\r'))
        {
            line[--n] = 0;
        }
        char* bar = strchr(line, '|');
        if(!bar)
        {
            continue;
        }
        *bar = 0;
        failures += decode_one(line, bar + 1);
    }
    fclose(list);
    return failures ? 1 : 0;
}
