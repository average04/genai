FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish KaraParty.SongScraper.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final

# System deps for ffmpeg + Python
RUN apt-get update && apt-get install -y --no-install-recommends \
    ffmpeg \
    python3 \
    python3-pip \
    python3-venv \
    && rm -rf /var/lib/apt/lists/*

# Install yt-dlp and demucs (demucs brings PyTorch CPU)
RUN python3 -m venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"
RUN pip install --no-cache-dir yt-dlp demucs

WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "KaraParty.SongScraper.dll"]
