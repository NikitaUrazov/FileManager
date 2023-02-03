using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Reflection;
using System.Configuration;

namespace FileManager
{
    internal class Program
    {
        static private List<string> ParsedUserInput = new List<string>();

        static private List<string> FileTree = new List<string>();
        static int pageSize;
        static int totalPages;
        static int currentPage = 0;

        static string currentDirectory = null;
        static private Errors error;

        static private StringBuilder sb = new StringBuilder();
        static private StringBuilder errorMessage = new StringBuilder();

        static private FileStream fileStream = null;

        static private FileSystemInfo entryInfo;

        private enum Errors
        {
            NoError = 0,
            EmptyQuotes,
            UnclosedQuotes,
            EmptyUserInput,
            DirectoryAlreadyExists,
            FileAlreadyExists,
            EntryNotFound,
            InvalidCommand,
            InvalidEntryName,
            DeleteError,
            CopyError,
            CreateError,
        }

        static void Main(string[] args)
        {
            Initialize();

            bool isExitCommand = false;

            Console.ForegroundColor = ConsoleColor.White;
            while (true)
            {
                Console.Clear();
                //Вывод дерева файлов
                ShowFileTree();
                Console.WriteLine();

                //Информация о файле/диретории
                ShowEntryInfo();

                //Информация о ошибке
                ShowErrorMessage();

                //Строка ввода
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(currentDirectory + ": ");
                Console.ForegroundColor = ConsoleColor.White;
                string userInput = GetUserInput();
                ParseUserInput(userInput, out error);

                //Выполнение команды
                if (error == Errors.NoError)
                    ExecuteCommand(out isExitCommand);

                if (isExitCommand)
                    break;
            }
        }

        /// <summary>
        /// Осуществляет вывод коммандной строки и посимвольное чтение ввода пользователя.
        /// Реализует функции специальных клавиш (стрелка вверх, стрелка вниз ...).
        /// </summary>
        /// <returns>Ввод пользователя</returns>
        static string GetUserInput()
        {
            ConsoleKeyInfo cki;
            sb.Clear();

            if (!File.Exists("CommandLog.txt"))
            {
                fileStream = File.Create("CommandLog.txt");
                fileStream.Close();
            }

            string[] commandLog = File.ReadAllLines("CommandLog.txt");

            int prevCommandIndex = -1;

            if (commandLog.Length != 0)
                prevCommandIndex = commandLog.Length;

            //Неудаляемая пользователем часть строки
            int deleteBorder = Console.CursorLeft;

            bool enterKeyIsPressed = false;


            while (!enterKeyIsPressed)
            {
                int i = Console.CursorLeft - deleteBorder;
                cki = Console.ReadKey(true);

                switch (cki.Key)
                {
                    case ConsoleKey.Enter:
                        enterKeyIsPressed = true;

                        if (sb.Length != 0)
                            File.AppendAllText("CommandLog.txt", sb.ToString() + '\n');

                        Console.WriteLine();
                        break;
                    case ConsoleKey.Backspace:
                        if (i > 0)
                        {
                            sb.Remove(i - 1, 1);
                            i--;
                            Console.CursorLeft = i + deleteBorder;
                            for (int j = i; j < sb.Length; j++)
                            {
                                Console.Write(sb[j]);
                            }
                            Console.Write(" \b");
                            Console.CursorLeft = i + deleteBorder;
                        }
                        break;
                    case ConsoleKey.UpArrow:
                        if (commandLog.Length != 0 && prevCommandIndex >= 0)
                        {
                            ////////////////////////////////////////////
                            Console.CursorLeft = sb.Length + deleteBorder;
                            while (Console.CursorLeft > deleteBorder)
                                Console.Write("\b \b");
                            Console.Write(" \b");
                            /////////////////////////////////////////////

                            if (prevCommandIndex != 0)
                                prevCommandIndex--;

                            sb.Clear();
                            sb.Append(commandLog[prevCommandIndex]);
                            Console.Write(sb.ToString());
                        }
                        break;
                    case ConsoleKey.DownArrow:
                        if (commandLog.Length != 0 && prevCommandIndex <= commandLog.Length - 1)
                        {
                            ////////////////////////////////////////////
                            Console.CursorLeft = sb.Length + deleteBorder;
                            while (Console.CursorLeft > deleteBorder)
                                Console.Write("\b \b");
                            Console.Write(" \b");
                            /////////////////////////////////////////////

                            if (prevCommandIndex != commandLog.Length - 1)
                                prevCommandIndex++;
                            sb.Clear();
                            sb.Append(commandLog[prevCommandIndex]);
                            Console.Write(sb.ToString());

                        }
                        break;
                    case ConsoleKey.LeftArrow:
                        if (i > 0)
                        {
                            Console.CursorLeft--;
                        }
                        break;
                    case ConsoleKey.RightArrow:
                        if (i < sb.Length)
                        {
                            Console.CursorLeft++;
                        }
                        break;
                    default:
                        // (Дополнительно) Нужна проверка на нужные символы (без F1, alt, тд...)
                        Console.Write(cki.KeyChar);
                        sb.Insert(i, cki.KeyChar);
                        int lastCursorPosition = Console.CursorLeft;
                        for (int j = i + 1; j < sb.Length; j++)
                            Console.Write(sb[j]);
                        Console.CursorLeft = lastCursorPosition;
                        break;
                }
            }


            return sb.ToString();
        }

        /// <summary>
        /// Осуществляет первоначальную инициализацию. Считывает параметры из конфигурационного файла.
        /// </summary>
        static void Initialize()
        {
            /*
            string sAttr = ConfigurationManager.AppSettings.Get("CurrentDirectory");

            if (sAttr.Length != 0 && Directory.Exists(sAttr))
                currentDirectory = sAttr;
            else
                currentDirectory = Assembly.GetExecutingAssembly().Location;

            entryInfo = new DirectoryInfo(currentDirectory);

            pageSize = 20;
            */

            var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var settings = configFile.AppSettings.Settings;

            if (settings["CurrentDirectory"].Value != "")
                currentDirectory = settings["CurrentDirectory"].Value;
            else
                currentDirectory = Assembly.GetExecutingAssembly().Location;

            entryInfo = new DirectoryInfo(currentDirectory);

            pageSize = int.Parse(settings["PageSize"].Value);
        }

        /// <summary>
        /// Осуществляет запись текущей директории в конфигурационный файл
        /// </summary>
        static void SaveCurrentDirectory()
        {
            var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var settings = configFile.AppSettings.Settings;

            settings["CurrentDirectory"].Value = currentDirectory;
            configFile.Save(ConfigurationSaveMode.Modified);
        }

        /// <summary>
        /// Осуществляет вывод информации о файле/директории в консоль
        /// </summary>
        static void ShowEntryInfo()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;

            Console.WriteLine("Имя : " + entryInfo.Name);
            Console.WriteLine("Время создания: " + entryInfo.CreationTime + " Измененно: " + entryInfo.LastWriteTime);
            //Аттрибуты
            Console.Write("Только для чтения: ");
            if ((entryInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                Console.WriteLine("Да");
            else
                Console.WriteLine("Нет");

            Console.Write("Размер: ");
            if (entryInfo is DirectoryInfo)
            {
                try
                {
                    Console.WriteLine(CalculateLength(entryInfo.FullName) + " байт");
                }
                catch (Exception e)
                {
                    Console.WriteLine("Доступ отказан.");
                }
            }
            else
            {
                Console.WriteLine(((FileInfo)entryInfo).Length + " байт");
            }

            Console.ForegroundColor = ConsoleColor.White;
        }

        /// <summary>
        /// Возвращает размер директоии в байтах
        /// </summary>
        /// <param name="directoryPath">Директория, размер которой нужно рассчитать</param>
        /// <returns></returns>
        static long CalculateLength(string directoryPath)
        {
            long length = 0;

            string[] directoryContent = Directory.GetFileSystemEntries(directoryPath);

            foreach (string entry in directoryContent)
            {
                if (Directory.Exists(entry))
                {
                    length += CalculateLength(entry);
                }
                else
                {
                    entryInfo = new FileInfo(entry);
                    length += ((FileInfo)entryInfo).Length;
                }
            }

            return length;
        }

        /// <summary>
        /// Выводит сообщение об ошибке.
        /// </summary>
        static void ShowErrorMessage()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            switch (error)
            {
                case Errors.NoError:
                    Console.WriteLine();
                    break;
                case Errors.DirectoryAlreadyExists:
                    Console.WriteLine("Директория с таким именем уже существует.");
                    break;
                case Errors.FileAlreadyExists:
                    Console.WriteLine("Файл с таким именем уже существует.");
                    break;
                case Errors.InvalidEntryName:
                    Console.WriteLine("Недопустимое имя.");
                    break;
                case Errors.UnclosedQuotes:
                    Console.WriteLine("Некорректный ввод. Не закрыты кавычки.");
                    break;
                case Errors.EmptyQuotes:
                    Console.WriteLine("Некорректный ввод. Пустые кавычки");
                    break;
                case Errors.EmptyUserInput:
                    break;
                case Errors.EntryNotFound:
                    Console.WriteLine("Директория/файл не найдены.");
                    break;
                case Errors.InvalidCommand:
                    Console.WriteLine("Неопознанная команда.");
                    break;
                case Errors.DeleteError:
                    Console.WriteLine("Ошибка удаления.");
                    break;
                case Errors.CreateError:
                    Console.WriteLine("Ошибка создания.");
                    break;
                case Errors.CopyError:
                    Console.WriteLine("Ошибка копирования.");
                    break;

            }

            if (errorMessage.Length != 0)
            {
                Console.WriteLine(errorMessage.ToString());
                errorMessage.Clear();
            }

            Console.ForegroundColor = ConsoleColor.White;
        }

        /// <summary>
        /// Распознаёт первое слово комманды пользователя и вызывает функции соответствующих комманд менеджера
        /// </summary>
        /// <param name="isExitCommand"></param>
        static void ExecuteCommand(out bool isExitCommand)
        {
            isExitCommand = false;
            switch (ParsedUserInput[0])
            {
                case "next":
                    ExecuteNextCommand();
                    break;
                case "prev":
                    ExecutePrevCommand();
                    break;
                case "page":
                    ExecutePageCommand();
                    break;
                case "cd":
                    ExecuteCdCommand();
                    break;
                case "mkdir":
                    ExecuteMkdirCommand();
                    break;
                case "cat":
                    ExecuteCatCommand();
                    break;
                case "copy":
                    ExecuteCopyCommand();
                    break;
                case "delete":
                    ExecuteDeleteCommand();
                    break;
                case "info":
                    ExecuteInfoCommand();
                    break;
                case "exit":
                    if (ParsedUserInput.Count == 1)
                        isExitCommand = true;
                    break;
            }
        }

        /// <summary>
        /// Осуществляет распознование и выполнение комманды info. Сохраняет информацию об ошибке в случае её наличия.
        /// </summary>
        static void ExecuteInfoCommand()
        {
            if (ParsedUserInput.Count != 2)
            {
                error = Errors.InvalidCommand;
                return;
            }

            string entryPath = Path.Combine(currentDirectory, ParsedUserInput[1]);

            if (Directory.Exists(entryPath))
                entryInfo = new DirectoryInfo(entryPath);
            else if (File.Exists(entryPath))
                entryInfo = new FileInfo(entryPath);
            else
            {
                error = Errors.EntryNotFound;
                errorMessage.AppendLine(entryPath);
            }
        }

        /// <summary>
        /// Осуществляет распознование и выполнение комманды copy. Сохраняет информацию об ошибке в случае её наличия.
        /// </summary>
        static void ExecuteCopyCommand()
        {
            if (ParsedUserInput.Count != 3)
            {
                error = Errors.InvalidCommand;
                return;
            }

            //Копируемый объект
            string original = Path.Combine(currentDirectory, ParsedUserInput[1]);

            //Копия объекта
            string copy = Path.Combine(currentDirectory, ParsedUserInput[2]);

            //Местонахождение копии
            string copyLocaltion = GetEntryLocation(copy);

            if (!Directory.Exists(copyLocaltion))
            {
                error = Errors.EntryNotFound;
                errorMessage.AppendLine(copyLocaltion);
                return;
            }

            if (Directory.Exists(original))
            {
                CopyDirectory(original, copy);
            }
            else if (File.Exists(original))
            {
                CopyFile(original, copy);
            }
            else
            {
                error = Errors.EntryNotFound;
                errorMessage.AppendLine(original);
            }
        }

        /// <summary>
        /// Осуществляет копирование директории и её содержимого. Сохраняет информацию об ошибке в случае её наличия.
        /// </summary>
        /// <param name="original">Путь оригианльной директории.</param>
        /// <param name="copy">Путь копии.</param>
        static void CopyDirectory(string original, string copy)
        {
            try
            {
                Directory.CreateDirectory(copy);

                string[] directoryContent = Directory.GetFileSystemEntries(original);

                if (directoryContent.Length == 0)
                    return;

                foreach (string entry in directoryContent)
                {
                    string entryName = entry.Split('\\')[entry.Split('\\').Length - 1];
                    string entryCopy = Path.Combine(copy, entryName);

                    if (Directory.Exists(entry))
                    {
                        CopyDirectory(entry, entryCopy);
                    }
                    else
                    {
                        CopyFile(entry, entryCopy);
                    }
                }
            }
            catch (Exception e)
            {
                error = Errors.CopyError;
                errorMessage.AppendLine(e.Message);
            }
        }

        /// <summary>
        /// Возвращает директорию, в которой находится файл/директория.
        /// </summary>
        /// <param name="entry">Файл/директория, директорию которых нужно получить.</param>
        /// <returns></returns>
        static string GetEntryLocation(string entry)
        {
            sb.Clear();
            for (int i = 0; i < entry.Split('\\').Length - 1; i++)
            {
                sb.Append(entry.Split('\\')[i] + '\\');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Осуществляет копирование файла. Сохраняет информацию об ошибке в случае её наличия
        /// </summary>
        /// <param name="original">Путь оригинала</param>
        /// <param name="copy">Путь копии</param>
        static void CopyFile(string original, string copy)
        {
            try
            {
                File.Copy(original, copy);
            }
            catch (Exception e)
            {
                error = Errors.CopyError;
                errorMessage.AppendLine(e.Message);
            }
        }

        /// Осуществляет распознование и выполнение комманды delete. Сохраняет информацию об ошибке в случае её наличия.

        static void ExecuteDeleteCommand()
        {
            if (ParsedUserInput.Count != 2)
            {
                error = Errors.InvalidCommand;
                return;
            }

            string entryToDelete = Path.Combine(currentDirectory, ParsedUserInput[1]);

            if (Directory.Exists(entryToDelete))
            {
                if (DeletionConfirmed(entryToDelete))
                    try
                    {
                        Directory.Delete(entryToDelete, true);
                    }
                    catch (Exception e)
                    {
                        error = Errors.DeleteError;
                        errorMessage.AppendLine(e.Message);
                    }

            }
            else if (File.Exists(entryToDelete))
            {
                if (DeletionConfirmed(entryToDelete))
                    try
                    {
                        File.Delete(entryToDelete);
                    }
                    catch (Exception e)
                    {
                        error = Errors.DeleteError;
                        errorMessage.AppendLine(e.Message);
                    }
            }
            else
            {
                error = Errors.EntryNotFound;
                errorMessage.AppendLine(entryToDelete);
            }
        }

        /// <summary>
        /// Осуществляет вывод запроса на подтверждение удаления файла/директории.
        /// </summary>
        /// <param name="entryToDelete">Удаляемый файл/диретктория</param>
        /// <returns></returns>
        static bool DeletionConfirmed(string entryToDelete)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("Удалить " + entryToDelete + "? (yes/no)");
                Console.WriteLine();
                string answer = Console.ReadLine();

                if (answer == "yes")
                    return true;
                else if (answer == "no")
                    return false;
            }
        }

        /// <summary>
        /// Осуществляет распознование и выполнение комманды next.
        /// </summary>
        static void ExecuteNextCommand()
        {
            if (ParsedUserInput.Count != 1)
            {
                error = Errors.InvalidCommand;
                return;
            }

            if (ParsedUserInput.Count == 1 && currentPage < totalPages)
                currentPage++;
        }

        /// <summary>
        /// Осуществляет распознование и выполнение комманды prev.
        /// </summary>
        static void ExecutePrevCommand()
        {
            if (ParsedUserInput.Count != 1)
            {
                error = Errors.InvalidCommand;
                return;
            }

            if (ParsedUserInput.Count == 1 && currentPage > 0)
                currentPage--;
        }

        /// <summary>
        /// Осуществляет распознование и выполнение комманды cat. Сохраняет информацию об ошибке в случае её наличия.
        /// </summary>
        static void ExecuteCatCommand()
        {
            if (ParsedUserInput.Count != 2)
            {
                error = Errors.InvalidCommand;
                return;
            }

            if (!IsEntryNameValid(ParsedUserInput[1]))
            {
                error = Errors.InvalidEntryName;
                return;
            }

            string newFile = Path.Combine(currentDirectory, ParsedUserInput[1]);

            if (!File.Exists(newFile))
                try
                {
                    fileStream = File.Create(newFile);
                    fileStream.Close();
                }
                catch (Exception e)
                {
                    error = Errors.CreateError;
                    errorMessage.AppendLine(e.Message);
                }
            else
                error = Errors.FileAlreadyExists;
        }

        /// <summary>
        /// Осуществляет распознование и выполнение комманды mkdir. Сохраняет информацию об ошибке в случае её наличия.
        /// </summary>
        static void ExecuteMkdirCommand()
        {
            if (ParsedUserInput.Count != 2)
            {
                error = Errors.InvalidCommand;
                return;
            }

            if (!IsEntryNameValid(ParsedUserInput[1]))
            {
                error = Errors.InvalidEntryName;
                return;
            }

            string newDirectory = Path.Combine(currentDirectory, ParsedUserInput[1]);

            if (!Directory.Exists(newDirectory))
                try
                {
                    Directory.CreateDirectory(newDirectory);
                }
                catch (Exception e)
                {
                    error = Errors.CreateError;
                    errorMessage.AppendLine(e.Message);
                }
            else
                error = Errors.DirectoryAlreadyExists;

        }

        /// <summary>
        /// Осуществляет проверку допустимости имени файла/директории.
        /// </summary>
        /// <param name="entryName">Имя файла/директории.</param>
        /// <returns></returns>
        static bool IsEntryNameValid(string entryName)
        {
            if (HasOnlySpaces(entryName))
                return false;

            if (entryName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Осуществляет проверку строки на содержание только пробелов.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        static bool HasOnlySpaces(string str)
        {
            foreach (char c in str)
                if (c != ' ')
                    return false;

            return true;
        }

        /// <summary>
        /// Выполняет команду пользователя, меняющую текущую директорию.
        /// </summary>
        static private void ExecuteCdCommand()

        {
            if (ParsedUserInput.Count != 2)
            {
                error = Errors.InvalidCommand;
                return;
            }

            string newDirectory = ParsedUserInput[1];

            if (newDirectory == "..")
            {
                string[] currentDirectoryArray = currentDirectory.Split('\\');
                if (currentDirectoryArray.Length > 1)
                {
                    sb.Clear();
                    for (int i = 0; i < currentDirectoryArray.Length - 1; i++)
                        sb.Append(currentDirectoryArray[i] + '\\');

                    sb.Remove(sb.Length - 1, 1);
                    currentDirectory = sb.ToString();
                    SaveCurrentDirectory();
                    entryInfo = new DirectoryInfo(currentDirectory);
                }
            }
            else if (Directory.Exists(Path.Combine(currentDirectory, newDirectory)))
            {
                currentDirectory = Path.Combine(currentDirectory, newDirectory);
                SaveCurrentDirectory();
                entryInfo = new DirectoryInfo(currentDirectory);
            }
            else
            {
                error = Errors.EntryNotFound;
                errorMessage.AppendLine(Path.Combine(currentDirectory, newDirectory));
            }

        }

        /// <summary>
        /// Выполняет команду пользователя, устанавливающую новое значение текущей страницы.
        /// </summary>
        static void ExecutePageCommand()
        {
            if (ParsedUserInput.Count != 2)
                return;

            if (!int.TryParse(ParsedUserInput[1], out int newPage))
                return;

            newPage--;
            if (newPage >= 0 && newPage <= totalPages)
                currentPage = newPage;
        }

        /// <summary>
        /// Преобразует ввод пользователя типа string к List<string>, элементами которого являются слова изначальной строки,
        /// и сохраняет в ParsedUserInput.
        /// </summary>
        /// <param name="userInput">Ввод пользователя</param>
        /// <param name="error">Код ошибки</param>
        static private void ParseUserInput(string userInput, out Errors error)
        {
            ParsedUserInput.Clear();
            bool openQuotes = false;
            error = Errors.NoError;

            if (userInput.Length == 0)
            {
                error = Errors.EmptyUserInput;
                return;
            }

            sb.Clear();

            for (int i = 0; i < userInput.Length; i++)
            {
                switch (userInput[i])
                {
                    //табуляция - действия как с пробелом, поэтому переход к case пробела
                    case '\t':
                    case ' ':
                        //Если кавычки открыты
                        if (openQuotes)
                            sb.Append(userInput[i]);
                        //Если кавычки закрыты
                        else if (sb.Length != 0)
                        {
                            ParsedUserInput.Add(sb.ToString());
                            sb.Clear();
                        }
                        break;

                    case '\"':
                        if (sb.Length != 0)
                        {
                            ParsedUserInput.Add(sb.ToString());
                            sb.Clear();
                        }
                        //Если между открывающими и закрывающими кавычками нет символов
                        else if (openQuotes)
                        {
                            error = Errors.EmptyQuotes;
                            return;
                        }

                        if (openQuotes)
                            openQuotes = false;
                        else
                            openQuotes = true;
                        break;

                    default:
                        sb.Append(userInput[i]);
                        break;
                }
            }

            //Если кавычки не закрыты
            if (openQuotes)
            {
                error = Errors.UnclosedQuotes;
                return;
            }

            if (sb.Length != 0)
                ParsedUserInput.Add(sb.ToString());

        }

        /// <summary>
        /// Выводит на экран дерево файлов и каталогов текущей директории.
        /// </summary>
        static private void ShowFileTree()
        {
            FileTree.Clear();
            int treeDepth = 1;
            SetFileTree(currentDirectory, treeDepth);

            //Если директория пуста
            if (FileTree.Count == 0)
            {
                for (int i = 0; i < pageSize; i++)
                    Console.WriteLine();

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Страница 1 из 1");
                Console.ForegroundColor = ConsoleColor.White;
                return;
            }

            totalPages = GetPagesAmmount();

            int rootEntriesLength = FileTree[0].Split('\\').Length;
            //Количество выведенных на экран строк
            int linesCount = 0;

            for (int i = currentPage * pageSize; i < FileTree.Count && i < (currentPage + 1) * pageSize; i++)
            {
                sb.Clear();
                int numberOfSpaces = FileTree[i].Split('\\').Length - rootEntriesLength;

                for (int j = 0; j < numberOfSpaces; j++)
                {
                    sb.Append("    ");
                }
                sb.Append(FileTree[i].Split('\\')[FileTree[i].Split('\\').Length - 1]);

                if (Directory.Exists(FileTree[i]))
                    Console.ForegroundColor = ConsoleColor.Yellow;

                Console.WriteLine(sb.ToString());

                Console.ForegroundColor = ConsoleColor.White;
                linesCount++;
            }

            //Заполнение страницы до конца пустыми строками (случается только на последней странице)
            for (int i = linesCount; i < pageSize; i++)
                Console.WriteLine();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Страница " + (currentPage + 1) + " из " + (totalPages + 1));
            Console.ForegroundColor = ConsoleColor.White;
        }

        /// <summary>
        /// Заполняет дерево файлов и каталогов.
        /// </summary>
        /// <param name="treeDepth">Глубина дерева.</param>
        static private void SetFileTree(string directory, int treeDepth)
        {
            try
            {
                foreach (string entry in Directory.GetFileSystemEntries(directory))
                {
                    FileTree.Add(entry);
                    if (Directory.Exists(entry) && treeDepth > 0)
                        SetFileTree(entry, treeDepth - 1);
                }
            }
            catch (Exception e)
            {

            }

        }

        /// <summary>
        /// Возвращает количество страниц необходимых для вывода элементов списка FileTree
        /// </summary>
        /// <returns></returns>
        static private int GetPagesAmmount()
        {
            int pagesAmmount;
            pagesAmmount = FileTree.Count / pageSize;
            if (FileTree.Count - pagesAmmount * pageSize != 0)
                pagesAmmount++;

            pagesAmmount--;
            return pagesAmmount;
        }
    }
}