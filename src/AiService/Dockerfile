FROM python:3.12-slim
WORKDIR /app
COPY src/AiService/requirements*.txt ./
RUN pip install --no-cache-dir -r requirements.txt
COPY src/AiService/app ./app
RUN useradd --create-home ai
USER ai
ENV PYTHONUNBUFFERED=1
EXPOSE 8000
HEALTHCHECK --interval=15s --timeout=3s CMD python -c "import urllib.request; urllib.request.urlopen('http://localhost:8000/ready')"
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000", "--workers", "2"]
