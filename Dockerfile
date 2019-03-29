# deps
FROM microsoft/dotnet:sdk
RUN apt-get update
RUN apt-get install -y python3 python3-pip

# checker reqs
WORKDIR /service
COPY  ./Checkers/enolib/requirements.txt ./Checkers/enolib/requirements.txt
RUN pip3 install -r "./Checkers/enolib/requirements.txt"
ENV PYTHONPATH "${PYTHONPATH}:/service/EnoEngine/out/Checkers/enolib"

# engine deps
COPY *.sln ./
COPY ./EnoEngine/EnoEngine.csproj ./EnoEngine/EnoEngine.csproj
RUN dotnet restore

# engine
COPY ./EnoEngine ./EnoEngine
COPY ./Checkers ./EnoEngine/out/Checkers
RUN dotnet publish -c Release -o out
WORKDIR /service/EnoEngine/out

ENTRYPOINT ["dotnet", "EnoEngine.dll"]
