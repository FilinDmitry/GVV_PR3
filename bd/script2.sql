USE [PR3_ValeraLox];
GO

-- Роли: как указано — user, manager
INSERT INTO [dbo].[Role] ([ID], [Name]) VALUES
(1, N'user'),
(2, N'manager');
GO

-- Статусы заказа: 1-оформлен, 2-в пути, 3-выдан
INSERT INTO [dbo].[Status] ([ID], [Name]) VALUES
(1, N'оформлен'),
(2, N'в пути'),
(3, N'выдан');
GO

-- Типы одежды: 5 строк
INSERT INTO [dbo].[Type] ([ID], [Name]) VALUES
(1, N'Футболка'),
(2, N'Куртка'),
(3, N'Джинсы'),
(4, N'Платье'),
(5, N'Обувь');
GO

-- Размеры: 5 строк
INSERT INTO [dbo].[Size] ([ID], [Size_str], [Size_int]) VALUES
(1, N'XS', 40),
(2, N'S',  42),
(3, N'M',  44),
(4, N'L',  46),
(5, N'XL', 48);
GO

-- Пользователи: 5 строк
INSERT INTO [dbo].[User] ([ID], [Adress], [Login], [PasswordHash], [RoleID], [Phone]) VALUES
(1, N'г. Москва, ул. Ленина, д. 1',                N'user1',    N'hash1', 1, N'+79000000001'),
(2, N'г. Москва, ул. Пушкина, д. 2',               N'user2',    N'hash2', 1, N'+79000000002'),
(3, N'г. Санкт-Петербург, Невский пр., д. 3',      N'user3',    N'hash3', 1, N'+79000000003'),
(4, N'г. Казань, ул. Баумана, д. 4',               N'manager1', N'hash4', 2, N'+79000000004'),
(5, N'г. Екатеринбург, ул. Мира, д. 5',            N'manager2', N'hash5', 2, N'+79000000005');
GO

-- Одежда: 5 строк
INSERT INTO [dbo].[Clothes] ([ID], [Name], [Description], [PurchaseAmount], [Type], [IsAvailiable], [Price]) VALUES
(1, N'Футболка Basic',  N'Хлопковая футболка',      10, 1, 1, 1500),
(2, N'Куртка Winter',   N'Тёплая зимняя куртка',     5, 2, 1, 7000),
(3, N'Джинсы Slim',     N'Синие джинсы',             7, 3, 1, 3500),
(4, N'Платье Evening',  N'Вечернее платье',          3, 4, 0, 9000),
(5, N'Кроссовки Sport', N'Спортивные кроссовки',    12, 5, 1, 4500);
GO

-- Товары: 5 строк
INSERT INTO [dbo].[Tovar] ([Clothes], [Size], [AmountAvailiable], [ID]) VALUES
(1, 1, 10, 1),
(2, 3,  5, 2),
(3, 4,  7, 3),
(4, 2,  3, 4),
(5, 5, 12, 5);
GO

-- Заказы: 5 строк
INSERT INTO [dbo].[Order] ([ID], [CreatedAt], [Status], [UserID]) VALUES
(1, '2026-10-01 10:00:00', 1, 1),
(2, '2026-10-02 11:30:00', 2, 2),
(3, '2026-10-03 12:15:00', 3, 3),
(4, '2026-10-04 13:45:00', 1, 4),
(5, '2026-10-05 14:20:00', 2, 5);
GO

-- Состав заказов: 5 строк
INSERT INTO [dbo].[TovarOrder] ([ID], [TovarID], [OrderID], [Amount]) VALUES
(1, 1, 1, 2),
(2, 2, 2, 1),
(3, 3, 3, 1),
(4, 4, 4, 3),
(5, 5, 5, 2);
GO