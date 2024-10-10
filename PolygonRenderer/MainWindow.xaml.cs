using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Linq;

namespace PolygonRenderer
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int currentPolygonIndex = -1; // Индекс текущего многоугольника, который рисуется
        private int currentLineIndex = 0;      // Индекс текущей линии многоугольника для рисования

        private List<Polygon> polygons = new List<Polygon>(); // Список многоугольников
        private double[,] zBuffer; // Z-буфер для отслеживания глубины

        public MainWindow()
        {
            InitializeComponent(); // Инициализация компонентов окна
            GeneratePolygons(); // Генерация случайных многоугольников
            zBuffer = new double[(int)Width, (int)Height]; // Создаем Z-буфер с размерами окна
            ClearZBuffer(); // Очищаем Z-буфер при старте
            polygons.Clear(); // Очищаем список многоугольников (можно убрать, если не нужно)
        }

        /// <summary>
        /// Генерация случайных многоугольников с произвольным количеством вершин.
        /// </summary>
        private void GeneratePolygons()
        {
            Random rand = new Random(); // Экземпляр генератора случайных чисел
            for (int i = 0; i < 6; i++)
            {
                int verticesCount = rand.Next(3, 7); // Случайное количество вершин от 3 до 6
                PointCollection vertices = new PointCollection(verticesCount); // Коллекция вершин
                for (int j = 0; j < verticesCount; j++)
                {
                    // Добавляем случайные координаты для каждой вершины
                    vertices.Add(new Point(rand.Next(0, (int)Width), rand.Next(0, (int)Height)));
                }
                // Создаем многоугольник с заданными вершинами и случайным цветом
                Polygon polygon = new Polygon
                {
                    Points = vertices,
                    Fill = new SolidColorBrush(Color.FromArgb(
                        (byte)rand.Next(256),
                        (byte)rand.Next(256),
                        (byte)rand.Next(256),
                        (byte)rand.Next(256))) // Задаем цвет с случайной прозрачностью и цветами
                };
                polygons.Add(polygon); // Добавляем многоугольник в список
            }
        }

        /// <summary>
        /// Очищает Z-буфер, устанавливая для всех его ячеек минимальные значения.
        /// </summary>
        private void ClearZBuffer()
        {
            for (int i = 0; i < Width; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    zBuffer[i, j] = float.NegativeInfinity; // Устанавливаем значение для каждой ячейки
                }
            }
        }

        /// <summary>
        /// Открывает диалог выбора файла и возвращает путь к выбранному файлу.
        /// </summary>
        private string OpenFileDialog()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*", // Фильтры для файловых расширений
                Title = "Select a Polygon File" // Заголовок диалогового окна
            };

            bool? result = openFileDialog.ShowDialog(); // Отображаем диалоговое окно

            if (result == true) // Если файл выбран
            {
                return openFileDialog.FileName; // Возвращаем путь к файлу
            }

            return string.Empty; // Возвращаем пустую строку, если файл не выбран
        }

        /// <summary>
        /// Загружает многоугольники из указанного файла.
        /// </summary>
        private void LoadPolygonsFromFile(string fileName)
        {
            //string[] lines = System.IO.File.ReadAllLines(fileName); // Читаем все строки из файла
            string[] lines = System.IO.File.ReadAllLines(fileName); // Считываем строки из файла

            Random rand = new Random(); // Генератор случайных цветов

            foreach (var line in lines)
            {
                // Убираем фигурные скобки из начала и конца строки и разбиваем по ';'
                string[] parts = line.Trim('{', '}').Split(';');

                PointCollection vertices = new PointCollection(); // Коллекция вершин для многоугольника
                float depth = 0; // Переменная для хранения глубины многоугольника

                // Обработка каждой части, если строка не пустая
                foreach (var part in parts)
                {
                    string[] coordinates = part.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (coordinates.Length == 2) // Проверяем наличие двух координат
                    {
                        if (float.TryParse(coordinates[0], out float x) &&
                            float.TryParse(coordinates[1], out float y))
                        {
                            vertices.Add(new Point(x, y)); // Добавляем вершину в коллекцию
                        }
                    }
                    else if (coordinates.Length == 1) // Если указана глубина
                    {
                        float.TryParse(coordinates[0], out depth); // Пробуем считать глубину
                    }
                }

                // Добавляем многоугольник только если есть организованные вершины
                if (vertices.Count > 0)
                {
                    byte r = (byte)rand.Next(256); // Случайный красный компонент
                    byte g = (byte)rand.Next(256); // Случайный зеленый компонент
                    byte b = (byte)rand.Next(256); // Случайный синий компонент

                    Polygon polygon = new Polygon
                    {
                        Points = vertices, // Устанавливаем вершины
                        Fill = new SolidColorBrush(Color.FromRgb(r, g, b)), // Задаем случайный цвет
                        Tag = depth // Устанавливаем глубину в Tag для дальнейшего использования
                    };

                    polygons.Add(polygon); // Добавляем многоугольник в список
                }
            }

            // Обновление отображаемого статуса после загрузки многоугольников
            ActiveEdgesStatus.Text = $"Загружено многоугольников: {polygons.Count}";
        }

        /// <summary>
        /// Рисует ребро между двумя точками.
        /// </summary>
        private void DrawEdge(Point p1, Point p2, Polygon polygon)
        {
            // Устанавливаем цвет для подсветки ребра
            Brush highlightBrush = new SolidColorBrush(Colors.Red);

            int dx = (int)(p2.X - p1.X); // Разница по X между двумя точками
            int dy = (int)(p2.Y - p1.Y); // Разница по Y между двумя точками
            int steps = Math.Max(Math.Abs(dx), Math.Abs(dy)); // Определяем количество шагов

            double xIncrement = dx / (double)steps; // Увеличение по X на каждом шаге
            double yIncrement = dy / (double)steps; // Увеличение по Y на каждом шаге

            double x = p1.X; // Начальное значение X
            double y = p1.Y; // Начальное значение Y

            // Получаем глубину текущего многоугольника
            float depth = (float)polygon.Tag; // Используем Tag для получения глубины

            for (int i = 0; i <= steps; i++)
            {
                int pixelX = (int)Math.Round(x); // Округляем X до ближайшего целого
                int pixelY = (int)Math.Round(y); // Округляем Y до ближайшего целого

                if (pixelX >= 0 && pixelX < DrawingCanvas.ActualWidth && pixelY >= 0 && pixelY < DrawingCanvas.ActualHeight)
                {
                    // Создаем маленький квадрат для рисования ребра
                    Rectangle rect = new Rectangle
                    {
                        Width = 1, // Ширина 1 пиксель
                        Height = 1, // Высота 1 пиксель
                        Fill = highlightBrush // Задаем цвет для подсветки
                    };

                    Canvas.SetLeft(rect, pixelX); // Устанавливаем позицию по X
                    Canvas.SetTop(rect, pixelY); // Устанавливаем позицию по Y
                    DrawingCanvas.Children.Add(rect); // Добавляем квадрат на канвас

                    // Обновление состояния Z-буфера
                    double currentZ = depth; // Используем глубину для обновления

                    // Проверяем и обновляем Z-буфер
                    if (currentZ > zBuffer[pixelX, pixelY]) // Если текущая глубина больше значения в Z-буфере
                    {
                        zBuffer[pixelX, pixelY] = currentZ; // Обновляем значение в Z-буфере
                        ZBufferStatus.Text = $"Z-буфер (x: {pixelX}, y: {pixelY}): {zBuffer[pixelX, pixelY]}"; // Показываем текущее состояние Z-буфера
                    }
                }

                x += xIncrement; // Увеличиваем x на значение xIncrement
                y += yIncrement; // Увеличиваем y на значение yIncrement
            }
        }

        /// <summary>
        /// Обрабатывает загрузку многоугольников из файла и отрисовывает их.
        /// </summary>
        private void LineScanAlgorithm(object sender, RoutedEventArgs e)
        {
            string fileName = OpenFileDialog(); // Получаем путь к файлу от пользователя
            if (!string.IsNullOrEmpty(fileName)) // Проверяем, что файл не пустой
            {
                LoadPolygonsFromFile(fileName); // Загружаем многоугольники из файла

                // Сортируем многоугольники по глубине (Tag)
                var sortedPolygons = polygons
                    .Where(p => p.Tag != null) // Убираем многоугольники с null Tag
                    .OrderBy(p => (float)p.Tag) // Сортируем по Tag
                    .ToList();

                // Очищаем канвас для новой отрисовки
                DrawingCanvas.Children.Clear();

                // Сбрасываем индексы многоугольников и линий
                currentPolygonIndex = 0; // Устанавливаем первый многоугольник
                currentLineIndex = 0; // Сбрасываем индекс линий

                // Обновление статуса после загрузки
                ActiveEdgesStatus.Text = $"Загружено многоугольников: {polygons.Count}";
            }
        }

        /// <summary>
        /// Закрашивает многоугольники на канвасе.
        /// </summary>
        private void FillButton_Click(object sender, RoutedEventArgs e)
        {
            // Сортируем многоугольники по глубине (Tag) перед закрашиванием
            var sortedPolygons = polygons.OrderBy(p => (float)p.Tag).ToList();

            foreach (var polygon in sortedPolygons)
            {
                Polygon filledPolygon = new Polygon
                {
                    Points = polygon.Points, // Устанавливаем точки
                    Fill = new SolidColorBrush(((SolidColorBrush)polygon.Fill).Color) // Закрашиваем многоугольник с используемым цветом
                };

                DrawingCanvas.Children.Add(filledPolygon); // Добавляем закрашенный многоугольник на канвас
            }
        }

        /// <summary>
        /// Постепенно рисует линии многоугольника.
        /// </summary>
        private void StepButton_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем, есть ли многоугольники в списке
            if (polygons.Count == 0)
            {
                ActiveEdgesStatus.Text = "Сначала загрузите многоугольники."; // Сообщение если нет многоугольников
                return;
            }

            // Получаем текущий многоугольник
            var polygon = polygons[currentPolygonIndex];
            var points = polygon.Points;

            // Проверяем, есть ли линии для рисования
            if (currentLineIndex < points.Count)
            {
                Point p1 = points[currentLineIndex]; // Текущая точка
                Point p2 = points[(currentLineIndex + 1) % points.Count]; // Следующая точка с учетом цикличности

                DrawEdge(p1, p2, polygon); // Рисуем линию между текущими точками

                // Обновляем индекс линии
                currentLineIndex++;
                ActiveEdgesStatus.Text = $"Рисуется линия {currentLineIndex} из {points.Count}.";
            }
            else
            {
                // Если все линии текущего многоугольника нарисованы
                ActiveEdgesStatus.Text = "Все линии текущего многоугольника нарисованы.";
                currentPolygonIndex++; // Переход к следующему многоугольнику
                currentLineIndex = 0; // Сброс индекса линий для следующего многоугольника

                // Проверяем, если достигли конца списка многоугольников, то начинаем сначала
                if (currentPolygonIndex >= polygons.Count)
                {
                    ActiveEdgesStatus.Text = "Все многоугольники были нарисованы.";
                    currentPolygonIndex = 0; // Можно реализовать другие действия по завершении
                }
            }
        }

        /// <summary>
        /// Очищает буфер и канвас от многоугольников.
        /// </summary>
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearZBuffer(); // Очищаем Z-буфер
            polygons.Clear(); // Очищаем список многоугольников
            DrawingCanvas.Children.Clear(); // Очищаем канвас
        }
    }
}
