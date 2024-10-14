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
    public partial class MainWindow : Window
    {
        private List<PolygonData> polygons = new List<PolygonData>(); // Список многоугольников
        private double[,] zBuffer; // Z-буфер для отслеживания глубины

        public MainWindow()
        {
            InitializeComponent(); // Инициализация компонентов окна
            zBuffer = new double[(int)Width, (int)Height]; // Инициализация Z-буфера
            ClearZBuffer(); // Очищаем Z-буфер при запуске
        }

        private void ClearZBuffer()
        {
            for (int i = 0; i < Width; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    zBuffer[i, j] = double.NegativeInfinity; // Устанавливаем начальное значение
                }
            }
        }

        private string OpenFileDialog()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                Title = "Select a Polygon File"
            };

            bool? result = openFileDialog.ShowDialog(); // Открываем диалог выбора файла
            return result == true ? openFileDialog.FileName : string.Empty;
        }

        private void LoadPolygonsFromFile(string fileName)
        {
            string[] lines = System.IO.File.ReadAllLines(fileName);
            Random rand = new Random();

            foreach (var line in lines)
            {
                string[] parts = line.Trim('{', '}').Split(';');
                List<Point> vertices = new List<Point>(); // Список вершин многоугольника

                foreach (var part in parts)
                {
                    string[] coordinates = part.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (coordinates.Length == 3 && float.TryParse(coordinates[0], out float x) &&
                        float.TryParse(coordinates[1], out float y) &&
                        float.TryParse(coordinates[2], out float z))
                    {
                        vertices.Add(new Point(x, y, z)); // Добавляем точку
                    }
                }

                if (vertices.Count > 0)
                {
                    PolygonData polygon = new PolygonData
                    {
                        Vertices = vertices,
                        Color = Color.FromRgb((byte)rand.Next(256), (byte)rand.Next(256), (byte)rand.Next(256)) // Генерация цвета
                    };
                    polygons.Add(polygon); // Добавляем многоугольник в список
                }
            }

            ActiveEdgesStatus.Text = $"Загружено многоугольников: {polygons.Count}"; // Обновляем статус
        }

        private void LineScanAlgorithm(object sender, RoutedEventArgs e)
        {
            string fileName = OpenFileDialog();
            if (!string.IsNullOrEmpty(fileName))
            {
                LoadPolygonsFromFile(fileName);
                DrawingCanvas.Children.Clear(); // Очищаем канвас
                foreach (var polygon in polygons)
                {
                    DrawPolygonEdges(polygon); // Рисуем границы многоугольников
                }
            }
        }

        private void DrawPolygonEdges(PolygonData polygon)
        {
            var vertices = polygon.Vertices;

            for (int i = 0; i < vertices.Count; i++)
            {
                Point p1 = vertices[i];
                Point p2 = vertices[(i + 1) % vertices.Count]; // Получаем следующую вершину
                DrawEdge(p1, p2, p1.Z); // Рисуем ребро
            }
        }

        private void DrawEdge(Point p1, Point p2, float depth)
        {
            Brush highlightBrush = new SolidColorBrush(Colors.Red); // Цвет ребра
            int dx = (int)(p2.X - p1.X);
            int dy = (int)(p2.Y - p1.Y);
            int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            double xIncrement = dx / (double)steps;
            double yIncrement = dy / (double)steps;

            double x = p1.X;
            double y = p1.Y;

            for (int i = 0; i <= steps; i++)
            {
                int pixelX = (int)Math.Round(x);
                int pixelY = (int)Math.Round(y);

                if (pixelX >= 0 && pixelX < DrawingCanvas.ActualWidth && pixelY >= 0 && pixelY < DrawingCanvas.ActualHeight)
                {
                    Rectangle rect = new Rectangle
                    {
                        Width = 1,
                        Height = 1,
                        Fill = highlightBrush // Рисуем пиксель
                    };

                    Canvas.SetLeft(rect, pixelX);
                    Canvas.SetTop(rect, pixelY);
                    DrawingCanvas.Children.Add(rect);

                    if (depth > zBuffer[pixelX, pixelY])
                    {
                        zBuffer[pixelX, pixelY] = depth; // Обновляем Z-буфер
                    }
                }

                x += xIncrement;
                y += yIncrement;
            }
        }

        private void FillButton_Click(object sender, RoutedEventArgs e)
        {
            DrawingCanvas.Children.Clear(); // Очищаем канвас
            ClearZBuffer(); // Очищаем Z-буфер для новой заливки

            // Сортируем многоугольники по глубине
            var sortedPolygons = polygons.OrderBy(p => p.Vertices.Average(v => v.Z)).ToList();

            foreach (var polygon in sortedPolygons)
            {
                DrawPolygon(polygon); // Рисуем многоугольник
            }
        }

        private void DrawPolygon(PolygonData polygon)
        {
            var vertices = polygon.Vertices.ToList();

            if (vertices.Count == 3)
            {
                DrawTriangle(vertices.ToArray(), polygon); // Рисуем треугольник
            }
            else if (vertices.Count == 4)
            {
                DrawTriangle(new[] { vertices[0], vertices[1], vertices[2] }, polygon);
                DrawTriangle(new[] { vertices[0], vertices[2], vertices[3] }, polygon); // Разбиваем на два треугольника
            }
            else
            {
                // Триангуляция многоугольников с 5 и более вершинами
                for (int i = 1; i < vertices.Count - 1; i++)
                {
                    DrawTriangle(new[] { vertices[0], vertices[i], vertices[i + 1] }, polygon);
                }
            }

            DrawCoordinates(polygon); // Отображение координат вершин
        }

        private void DrawTriangle(Point[] triangle, PolygonData polygon)
        {
            var vertices = triangle.OrderBy(v => v.Y).ToList(); // Сортировка по Y

            int minY = (int)Math.Ceiling(vertices[0].Y); // Минимальная Y-координата
            int maxY = (int)Math.Floor(vertices[2].Y); // Максимальная Y-координата

            for (int y = minY; y <= maxY; y++)
            {
                List<double> intersections = new List<double>();

                for (int i = 0; i < vertices.Count; i++)
                {
                    var p1 = vertices[i];
                    var p2 = vertices[(i + 1) % vertices.Count];

                    if ((p1.Y <= y && p2.Y > y) || (p2.Y <= y && p1.Y > y))
                    {
                        double x = p1.X + (y - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y);
                        intersections.Add(x); // Находим пересечения
                    }
                }

                intersections.Sort();
                for (int i = 0; i < intersections.Count; i += 2) // Рисуем пиксели между пересечениями
                {
                    int xStart = (int)Math.Ceiling(intersections[i]);
                    int xEnd = (int)Math.Floor(intersections[i + 1]);
                    for (int x = xStart; x <= xEnd; x++)
                    {
                        double z = GetDepthAtPoint(x, y, new PolygonData { Vertices = new List<Point> { triangle[0], triangle[1], triangle[2] } });
                        if (z > zBuffer[x, y])
                        {
                            zBuffer[x, y] = z; // Обновляем Z-буфер
                            DrawPixel(x, y, polygon.Color); // Рисуем пиксель
                        }
                    }
                }
            }
        }

        private void DrawCoordinates(PolygonData polygon)
        {
            foreach (var vertex in polygon.Vertices)
            {
                TextBlock textBlock = new TextBlock
                {
                    Text = $"({vertex.X}, {vertex.Y}, {vertex.Z})", // Координаты вершины
                    Foreground = Brushes.Black,
                    FontWeight = FontWeights.Bold
                };

                Canvas.SetLeft(textBlock, vertex.X + 5); // Позиция текста
                Canvas.SetTop(textBlock, vertex.Y - 15); // Позиция текста

                DrawingCanvas.Children.Add(textBlock); // Добавляем текст на канвас
            }
        }

        private double GetDepthAtPoint(int x, int y, PolygonData polygon)
        {
            // Используем барицентрические координаты для нахождения Z
            var vertices = polygon.Vertices;

            var p1 = vertices[0];
            var p2 = vertices[1];
            var p3 = vertices[2];

            double area = 0.5 * (-p2.Y * p3.X + p1.Y * (-p2.X + p3.X) + p1.X * (p2.Y - p3.Y) + p2.X * p3.Y);
            double s = 1 / (2 * area) * (p1.Y * p3.X - p1.X * p3.Y + (p3.Y - p1.Y) * x + (p1.X - p3.X) * y);
            double t = 1 / (2 * area) * (p1.X * p2.Y - p1.Y * p2.X + (p1.Y - p2.Y) * x + (p2.X - p1.X) * y);

            if (s >= 0 && t >= 0 && (s + t) <= 1) // Проверка на принадлежность треугольнику
            {
                return p1.Z * (1 - s - t) + p2.Z * s + p3.Z * t; // Возвращаем Z
            }

            return double.NegativeInfinity; // Вне треугольника
        }

        private void DrawPixel(int x, int y, Color color)
        {
            Rectangle rect = new Rectangle
            {
                Width = 1,
                Height = 1,
                Fill = new SolidColorBrush(color) // Цвет пикселя
            };

            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            DrawingCanvas.Children.Add(rect); // Добавляем пиксель на канвас
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ClearZBuffer();
            polygons.Clear(); // Очищаем список многоугольников
            DrawingCanvas.Children.Clear(); // Очищаем канвас
        }
    }

    public class PolygonData
    {
        public List<Point> Vertices { get; set; } // Вершины многоугольника
        public Color Color { get; set; } // Цвет многоугольника
    }

    public struct Point
    {
        public float X;
        public float Y;
        public float Z;

        public Point(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z; // Инициализация координат
        }
    }
}
